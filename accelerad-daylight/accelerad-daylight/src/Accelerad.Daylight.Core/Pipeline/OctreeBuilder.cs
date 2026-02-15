using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Builds an octree from scene files + sky dome using oconv.
/// The sky dome is a glow source covering the full hemisphere,
/// used as the receiver for daylight coefficient calculations.
/// </summary>
public class OctreeBuilder : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public OctreeBuilder(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.OctreeBuilding;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        // Write sky dome receiver file
        var skyDomePath = Path.Combine(context.TempDir, "skydome.rad");
        await WriteSkyDome(skyDomePath);

        // Build octree: oconv -f scene.rad [scene2.rad ...] skydome.rad > scene.oct
        var octreePath = Path.Combine(context.TempDir, "scene.oct");
        var sceneArgs = string.Join(" ",
            context.SceneFiles.Select(f => $"\"{f}\""));

        await _runner.RunToFileAsync(
            "oconv",
            $"-f {sceneArgs} \"{skyDomePath}\"",
            octreePath);

        context.OctreePath = octreePath;
        return context;
    }

    /// <summary>
    /// Write a sky dome Radiance file with a single glow source.
    /// Uses a full-sphere source so rcontrib tracks a single modifier,
    /// producing a DC matrix whose columns match gendaymtx rows.
    /// </summary>
    private static async Task WriteSkyDome(string path)
    {
        const string skyDome = """
            void glow sky_glow
            0
            0
            4 1 1 1 0

            sky_glow source sky
            0
            0
            4 0 0 1 360
            """;
        await File.WriteAllTextAsync(path, skyDome);
    }
}
