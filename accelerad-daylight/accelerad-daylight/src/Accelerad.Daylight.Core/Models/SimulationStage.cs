namespace Accelerad.Daylight.Core.Models;

public enum SimulationStage
{
    WeatherConversion,
    SceneConversion,
    OctreeBuilding,
    DaylightCoefficients,
    SkyMatrix,
    AnnualMultiplication,
    PostProcessing,
    WritingResults
}
