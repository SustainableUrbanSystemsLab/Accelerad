using System.Text.RegularExpressions;
using Accelerad.Daylight.Core.Infrastructure;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Pipeline;

/// <summary>
/// Converts OBJ geometry to Radiance scene format using obj2rad.
/// Automatically generates default plastic materials for any OBJ groups
/// that obj2rad references as modifiers (they would otherwise be undefined).
/// Only runs when an OBJ file is provided (Config.HasObjFile).
/// </summary>
public class SceneConverter : IPipelineStage
{
    private readonly RadianceProcessRunner _runner;

    public SceneConverter(RadianceProcessRunner runner) => _runner = runner;

    public SimulationStage Stage => SimulationStage.SceneConversion;

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context)
    {
        if (!context.Config.HasObjFile)
            return context; // Skip if using .rad scene files directly

        var rawRadPath = Path.Combine(context.TempDir, "scene_raw.rad");
        var sceneRadPath = Path.Combine(context.TempDir, "scene_from_obj.rad");

        // obj2rad reads OBJ and writes Radiance scene description to stdout
        await _runner.RunToFileAsync(
            "obj2rad",
            $"\"{context.Config.ObjFilePath}\"",
            rawRadPath);

        // obj2rad uses OBJ group names as Radiance modifiers but doesn't define them.
        // Scan for modifier names and prepend default plastic material definitions.
        var rawContent = await File.ReadAllTextAsync(rawRadPath);
        var modifiers = FindUndefinedModifiers(rawContent);

        using var writer = new StreamWriter(sceneRadPath);
        // Write default materials for each modifier
        foreach (var mod in modifiers)
        {
            // Default: 50% reflectance grey plastic
            await writer.WriteLineAsync($"void plastic {mod}");
            await writer.WriteLineAsync("0");
            await writer.WriteLineAsync("0");
            await writer.WriteLineAsync("5 0.5 0.5 0.5 0 0");
            await writer.WriteLineAsync();
        }
        // Append the obj2rad output
        await writer.WriteAsync(rawContent);

        context.SceneRadPath = sceneRadPath;
        return context;
    }

    /// <summary>
    /// Find modifier names used in polygon definitions that are not
    /// defined elsewhere in the file (i.e., not "void" and not already
    /// defined as a material/texture).
    /// </summary>
    private static HashSet<string> FindUndefinedModifiers(string content)
    {
        var defined = new HashSet<string> { "void" };
        var used = new HashSet<string>();

        // Radiance format: "modifier type name" on a line
        // Materials define names: "void plastic foo" → "foo" is defined
        // Geometry uses modifiers: "foo polygon foo.1" → "foo" is used
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var modifier = parts[0];
                var type = parts[1];
                var name = parts[2];

                // Material/texture definition → name is now defined
                if (type is "plastic" or "metal" or "trans" or "glass" or "dielectric"
                    or "light" or "glow" or "spotlight" or "mirror" or "texfunc" or "texdata"
                    or "colorfunc" or "brightfunc" or "colorpict" or "colortext" or "brighttext")
                {
                    defined.Add(name);
                }

                // Geometry → modifier is used
                if (type is "polygon" or "sphere" or "cylinder" or "cone" or "ring"
                    or "source" or "bubble" or "cup" or "tube")
                {
                    used.Add(modifier);
                }
            }
        }

        used.ExceptWith(defined);
        return used;
    }
}

