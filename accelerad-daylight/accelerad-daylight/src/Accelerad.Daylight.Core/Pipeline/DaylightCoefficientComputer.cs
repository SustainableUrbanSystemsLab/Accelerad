using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;
using System.Globalization;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Computes daylight coefficients using rcontrib.
/// Traces rays from sensor points through the scene and records
/// contributions from each Reinhart sky patch.
/// </summary>
public class DaylightCoefficientComputer : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public DaylightCoefficientComputer(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.DaylightCoefficients;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        var config = context.Config;
        var dcMatrixPath = Path.Combine(context.TempDir, "dc_matrix.mtx");

        // Write sensor points to file for stdin
        var sensorFilePath = Path.Combine(context.TempDir, "sensors.txt");
        await context.SensorGrid.WriteToFileAsync(sensorFilePath);

        // Calculate Nrbins for the Reinhart subdivision
        // Formula: MF*MF*144 + 1 (for sky) + 1 (for ground) when including ground
        // rcontrib uses reinhartb.cal which computes this via Nrbins variable

        // Build rcontrib arguments:
        // -I+       : irradiance mode (sensor points with normals)
        // -ab N     : ambient bounces
        // -ad N     : ambient divisions
        // -lw N     : limit weight
        // -c 1      : one contribution record per input ray
        // -e "MF:N" : set Reinhart subdivision factor
        // -f reinhartb.cal : Reinhart basis function
        // -b rbin   : bin expression (from reinhartb.cal)
        // -bn Nrbins: number of bins (from reinhartb.cal)
        // -m sky_glow  : track contributions to sky
        // -m ground_glow : track contributions to ground
        // -faa      : ASCII input, ASCII output (for debugging; use -faf for production)

        // Create a custom cal file that maps ground rays to bin 0 and shifts sky rays by 1.
        var nrbins = 144 * config.ReinhartMF * config.ReinhartMF + 2;
        var calPath = Path.Combine(context.TempDir, "reinhart_mapped.cal");
        // Radiance if(cond, then, else): cond > 0 is true.
        // We want Dz < -1e-6 to be true. So cond = -1e-6 - Dz > 0 implies Dz < -1e-6.
        await File.WriteAllTextAsync(
            calPath,
            $"Nrbins = {nrbins};{Environment.NewLine}bin = if(-1e-6 - Dz, 0, rbin + 1);");

        var args = $"-I+ " +
                   $"-ab {config.AmbientBounces} " +
                   $"-ad {config.AmbientDivisions} " +
                   $"-lw {config.LimitWeight.ToString("G", CultureInfo.InvariantCulture)} " +
                   $"-c 1 " +
                   $"-f reinhartb.cal " +
                   $"-e MF={config.ReinhartMF} " +
                   $"-f \"{calPath}\" " +
                   $"-b bin -bn Nrbins " +
                   $"-m sky_glow " +
                   $"-faf " +
                   $"\"{context.OctreePath}\"";

        await _runner.RunToFileAsync(
            "rcontrib", args, dcMatrixPath,
            stdinFile: sensorFilePath);

        context.DcMatrixPath = dcMatrixPath;
        return context;
    }
}
