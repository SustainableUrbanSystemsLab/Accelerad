using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.IO;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Orchestrates the full annual daylight simulation pipeline.
/// Runs stages in sequence, passing context between them.
/// </summary>
public class PipelineOrchestrator
{
    private readonly SimulationConfig _config;
    private readonly IProgress<SimulationStage>? _progress;

    public PipelineOrchestrator(SimulationConfig config, IProgress<SimulationStage>? progress = null)
    {
        _config = config;
        _progress = progress;
    }

    public async Task<AnnualResults> RunAsync()
    {
        Validate();

        var binaryFinder = new BinaryFinder(_config.RadianceBinDir);
        var missing = binaryFinder.ValidateRequiredBinaries();
        if (missing.Count > 0)
        {
            throw new FileNotFoundException(
                $"Missing Radiance executables: {string.Join(", ", missing)}. " +
                "Install Radiance or set --radiance-dir.");
        }

        var runner = new RadianceProcessRunner(binaryFinder);
        var sensorGrid = await SensorGridReader.ReadAsync(_config.SensorGridPath);

        using var tempDir = new TempDirectoryManager(_config.KeepTemp);

        var context = new PipelineContext
        {
            Config = _config,
            TempDir = tempDir.TempDir,
            SensorGrid = sensorGrid
        };

        // Build and execute pipeline stages
        var stages = BuildStages(runner);

        foreach (var stage in stages)
        {
            _progress?.Report(stage.Stage);
            context = await stage.ExecuteAsync(context);
        }

        // Parse results
        _progress?.Report(SimulationStage.WritingResults);
        var results = await IlluminanceResultsParser.ParseAsync(
            context.IlluminancePath!, sensorGrid);

        // Write output
        if (_config.OutputFormat == "binary")
            await ResultsWriter.WriteBinaryAsync(results, _config.OutputPath);
        else
            await ResultsWriter.WriteCsvAsync(results, _config.OutputPath);

        if (_config.KeepTemp)
        {
            Console.Error.WriteLine($"Intermediate files retained at: {tempDir.TempDir}");
        }

        return results;
    }

    private List<IPipelineStage> BuildStages(RadianceProcessRunner runner)
    {
        var stages = new List<IPipelineStage>
        {
            new SceneConverter(runner),
            new WeatherConverter(runner),
            new OctreeBuilder(runner),
            new DaylightCoefficientComputer(runner),
            new SkyMatrixGenerator(runner),
            new AnnualMultiplier(runner),
            new PostProcessor(runner)
        };
        return stages;
    }

    private void Validate()
    {
        if (!_config.HasSceneFiles && !_config.HasObjFile)
            throw new ArgumentException("Either --scene or --obj must be provided.");

        if (!File.Exists(_config.EpwFilePath))
            throw new FileNotFoundException($"EPW file not found: {_config.EpwFilePath}");

        if (!File.Exists(_config.SensorGridPath))
            throw new FileNotFoundException($"Sensor grid file not found: {_config.SensorGridPath}");

        if (_config.HasSceneFiles)
        {
            foreach (var f in _config.SceneFiles)
            {
                if (!File.Exists(f))
                    throw new FileNotFoundException($"Scene file not found: {f}");
            }
        }

        if (_config.HasObjFile && !File.Exists(_config.ObjFilePath!))
            throw new FileNotFoundException($"OBJ file not found: {_config.ObjFilePath}");
    }
}
