using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Multiplies daylight coefficient matrix by sky matrix using dctimestep.
/// Produces annual hourly results: DC[sensors x sky_patches] × Sky[sky_patches x 8760]
/// = Results[sensors x 8760].
/// </summary>
public class AnnualMultiplier : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public AnnualMultiplier(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.AnnualMultiplication;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        var annualPath = Path.Combine(context.TempDir, "annual_raw.mtx");

        // dctimestep dc_matrix.mtx sky_matrix.smx > annual_raw.mtx
        var args = $"\"{context.DcMatrixPath}\" \"{context.SkyMatrixPath}\"";

        await _runner.RunToFileAsync(
            "dctimestep", args, annualPath);

        context.AnnualResultsPath = annualPath;
        return context;
    }
}
