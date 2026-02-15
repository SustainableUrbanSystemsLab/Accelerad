using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

public interface IPipelineStage
{
    SimulationStage Stage { get; }
    Task<PipelineContext> ExecuteAsync(PipelineContext context);
}

/// <summary>
/// Carries intermediate file paths between pipeline stages.
/// </summary>
public class PipelineContext
{
    public required SimulationConfig Config { get; init; }
    public required string TempDir { get; init; }
    public required SensorGrid SensorGrid { get; init; }

    // Intermediate file paths set by stages
    public string? WeaFilePath { get; set; }
    public string? SceneRadPath { get; set; }
    public string? OctreePath { get; set; }
    public string? DcMatrixPath { get; set; }
    public string? SkyMatrixPath { get; set; }
    public string? AnnualResultsPath { get; set; }
    public string? IlluminancePath { get; set; }

    /// <summary>Scene .rad files (from config or converted from OBJ).</summary>
    public string[] SceneFiles => Config.HasSceneFiles
        ? Config.SceneFiles
        : SceneRadPath != null
            ? new[] { SceneRadPath }
            : throw new InvalidOperationException("No scene files available");
}
