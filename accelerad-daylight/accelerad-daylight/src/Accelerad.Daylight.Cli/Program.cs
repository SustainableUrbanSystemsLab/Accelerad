using System.CommandLine;
using Accelerad.Daylight.Core.Models;
using Accelerad.Daylight.Core.Pipeline;

var sceneOption = new Option<string[]?>(
    "--scene",
    "Radiance scene files (.rad)")
{ AllowMultipleArgumentsPerToken = true };

var objOption = new Option<string?>(
    "--obj",
    "OBJ geometry file (alternative to --scene)");

var epwOption = new Option<string>(
    "--epw",
    "EPW weather file path")
{ IsRequired = true };

var sensorsOption = new Option<string>(
    "--sensors",
    "Sensor grid CSV file (x,y,z,nx,ny,nz per line)")
{ IsRequired = true };

var outputOption = new Option<string>(
    "--output",
    () => "./results",
    "Output directory for results");

var radianceDirOption = new Option<string?>(
    "--radiance-dir",
    "Path to Radiance/Accelerad bin directory (auto-detected if omitted)");

var mfOption = new Option<int>(
    "--mf",
    () => 4,
    "Reinhart sky subdivision factor (default: 4 → 2305 sky patches)");

var abOption = new Option<int>(
    "--ab",
    () => 5,
    "Ambient bounces");

var adOption = new Option<int>(
    "--ad",
    () => 4096,
    "Ambient divisions");

var formatOption = new Option<string>(
    "--format",
    () => "csv",
    "Output format: csv or binary");

var keepTempOption = new Option<bool>(
    "--keep-temp",
    () => false,
    "Retain intermediate files for debugging");

var runCommand = new Command("run", "Run annual daylight simulation")
{
    sceneOption, objOption, epwOption, sensorsOption, outputOption,
    radianceDirOption, mfOption, abOption, adOption, formatOption, keepTempOption
};

runCommand.SetHandler(async (context) =>
{
    var scene = context.ParseResult.GetValueForOption(sceneOption);
    var obj = context.ParseResult.GetValueForOption(objOption);
    var epw = context.ParseResult.GetValueForOption(epwOption)!;
    var sensors = context.ParseResult.GetValueForOption(sensorsOption)!;
    var output = context.ParseResult.GetValueForOption(outputOption)!;
    var radianceDir = context.ParseResult.GetValueForOption(radianceDirOption);
    var mf = context.ParseResult.GetValueForOption(mfOption);
    var ab = context.ParseResult.GetValueForOption(abOption);
    var ad = context.ParseResult.GetValueForOption(adOption);
    var format = context.ParseResult.GetValueForOption(formatOption)!;
    var keepTemp = context.ParseResult.GetValueForOption(keepTempOption);

    if (scene == null && obj == null)
    {
        Console.Error.WriteLine("Error: Either --scene or --obj must be provided.");
        context.ExitCode = 1;
        return;
    }

    var config = new SimulationConfig
    {
        SceneFiles = scene ?? Array.Empty<string>(),
        ObjFilePath = obj,
        EpwFilePath = epw,
        SensorGridPath = sensors,
        OutputPath = output,
        RadianceBinDir = radianceDir,
        ReinhartMF = mf,
        AmbientBounces = ab,
        AmbientDivisions = ad,
        OutputFormat = format,
        KeepTemp = keepTemp
    };

    var progress = new Progress<SimulationStage>(stage =>
    {
        var label = stage switch
        {
            SimulationStage.WeatherConversion => "Converting weather data (EPW → WEA)",
            SimulationStage.SceneConversion => "Converting scene (OBJ → Radiance)",
            SimulationStage.OctreeBuilding => "Building octree",
            SimulationStage.DaylightCoefficients => "Computing daylight coefficients (this may take a while)",
            SimulationStage.SkyMatrix => "Generating annual sky matrix",
            SimulationStage.AnnualMultiplication => "Computing annual results",
            SimulationStage.PostProcessing => "Converting to illuminance",
            SimulationStage.WritingResults => "Writing results",
            _ => stage.ToString()
        };
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] {label}...");
    });

    try
    {
        var orchestrator = new PipelineOrchestrator(config, progress);
        var results = await orchestrator.RunAsync();

        Console.Error.WriteLine();
        Console.Error.WriteLine($"Simulation complete.");
        Console.Error.WriteLine($"  Sensors: {results.SensorCount}");
        Console.Error.WriteLine($"  Hours: {results.HourCount}");
        Console.Error.WriteLine($"  Output: {Path.GetFullPath(output)}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (keepTemp)
            Console.Error.WriteLine("Intermediate files have been retained for debugging.");
        context.ExitCode = 1;
    }
});

var rootCommand = new RootCommand("Accelerad Daylight - Annual daylight simulation CLI")
{
    runCommand
};

return await rootCommand.InvokeAsync(args);
