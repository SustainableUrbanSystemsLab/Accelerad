using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Post-processes raw annual results using rmtxop.
/// Converts RGB radiance triplets to photopic illuminance (lux)
/// using the standard luminous efficacy coefficients: 47.4, 119.9, 11.6.
/// </summary>
public class PostProcessor : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public PostProcessor(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.PostProcessing;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        var illuminancePath = Path.Combine(context.TempDir, "annual_illuminance.txt");

        // rmtxop -fa -c 47.4 119.9 11.6 annual_raw.mtx > illuminance.txt
        // -fa : ASCII float output
        // -c  : component transformation (RGB → single illuminance channel)
        // Coefficients: 179 * (0.265, 0.670, 0.065) ≈ 47.4, 119.9, 11.6
        var args = $"-fa -c 47.4 119.9 11.6 \"{context.AnnualResultsPath}\"";

        await _runner.RunToFileAsync(
            "rmtxop", args, illuminancePath);

        context.IlluminancePath = illuminancePath;
        return context;
    }
}
