using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Generates the annual sky matrix from weather data using gendaymtx.
/// Produces a sky_patches x 8760 matrix of sky radiance values.
/// </summary>
public class SkyMatrixGenerator : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public SkyMatrixGenerator(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.SkyMatrix;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        var skyMatrixPath = Path.Combine(context.TempDir, "sky_matrix.smx");

        // gendaymtx -m MF -of weather.wea > sky.smx
        // -m N : Reinhart subdivision (must match rcontrib MF)
        // -of  : binary float output (faster than ASCII)
        var args = $"-m {context.Config.ReinhartMF} -of \"{context.WeaFilePath}\"";

        await _runner.RunToFileAsync(
            "gendaymtx", args, skyMatrixPath);

        context.SkyMatrixPath = skyMatrixPath;
        return context;
    }
}
