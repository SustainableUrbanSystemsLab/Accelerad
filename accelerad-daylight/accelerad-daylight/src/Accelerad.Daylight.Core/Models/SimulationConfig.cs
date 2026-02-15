namespace Accelerad.Daylight.Core.Models;

public class SimulationConfig
{
    /// <summary>Radiance scene files (.rad). Used when --scene is provided.</summary>
    public string[] SceneFiles { get; set; } = Array.Empty<string>();

    /// <summary>OBJ geometry file. Used when --obj is provided (Phase 2).</summary>
    public string? ObjFilePath { get; set; }

    /// <summary>EPW weather file path (required).</summary>
    public required string EpwFilePath { get; set; }

    /// <summary>Sensor grid CSV file path (required).</summary>
    public required string SensorGridPath { get; set; }

    /// <summary>Output directory for results.</summary>
    public required string OutputPath { get; set; }

    /// <summary>Path to Radiance/Accelerad binary directory. Auto-detected if null.</summary>
    public string? RadianceBinDir { get; set; }

    /// <summary>Reinhart sky subdivision factor (default: 4 → 2305 patches).</summary>
    public int ReinhartMF { get; set; } = 4;

    /// <summary>Ambient bounces (-ab parameter).</summary>
    public int AmbientBounces { get; set; } = 5;

    /// <summary>Ambient divisions (-ad parameter).</summary>
    public int AmbientDivisions { get; set; } = 4096;

    /// <summary>Limit weight for ray tracing (-lw parameter).</summary>
    public double LimitWeight { get; set; } = 0.0002;

    /// <summary>Output format: "csv" or "binary".</summary>
    public string OutputFormat { get; set; } = "csv";

    /// <summary>If true, retain temporary intermediate files for debugging.</summary>
    public bool KeepTemp { get; set; }

    public bool HasSceneFiles => SceneFiles.Length > 0;
    public bool HasObjFile => !string.IsNullOrEmpty(ObjFilePath);
}
