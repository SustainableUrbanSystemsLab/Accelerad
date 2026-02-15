using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Converts EPW weather file to WEA format using epw2wea.
/// </summary>
public class WeatherConverter : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public WeatherConverter(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.WeatherConversion;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        var weaPath = Path.Combine(context.TempDir, "weather.wea");
        var result = await _runner.RunAsync(
            "epw2wea",
            $"\"{context.Config.EpwFilePath}\" \"{weaPath}\"");
        result.ThrowIfFailed();

        context.WeaFilePath = weaPath;
        return context;
    }
}
