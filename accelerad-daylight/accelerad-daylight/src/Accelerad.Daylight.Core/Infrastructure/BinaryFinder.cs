using System.Runtime.InteropServices;

namespace Accelerad.Daylight.Core.Infrastructure;

/// <summary>
/// Locates Radiance/Accelerad executables on the system.
/// Search order: explicit path → RAYPATH env → common install paths → PATH.
/// Prefers accelerad_* variants (GPU) over standard Radiance binaries (CPU).
/// </summary>
public class BinaryFinder
{
    private readonly string? _explicitBinDir;
    private string? _resolvedBinDir;

    /// <summary>Required executables for the annual daylight pipeline.</summary>
    public static readonly string[] RequiredBinaries = new[]
    {
        "oconv", "rcontrib", "gendaymtx", "dctimestep", "rmtxop", "epw2wea"
    };

    public BinaryFinder(string? explicitBinDir = null)
    {
        _explicitBinDir = explicitBinDir;
    }

    /// <summary>
    /// Get the path to a specific Radiance executable.
    /// For rcontrib, prefers accelerad_rcontrib if available.
    /// </summary>
    public string GetBinaryPath(string binaryName)
    {
        var binDir = GetBinDir();

        // For rcontrib, try GPU-accelerated version first
        if (binaryName == "rcontrib")
        {
            var accelPath = FindExecutable(binDir, "accelerad_rcontrib");
            if (accelPath != null)
                return accelPath;
        }

        var path = FindExecutable(binDir, binaryName);
        if (path != null)
            return path;

        throw new FileNotFoundException(
            $"Could not find Radiance executable '{binaryName}'. " +
            $"Searched in: {binDir ?? "system PATH"}. " +
            "Set --radiance-dir or the RAYPATH environment variable.");
    }

    /// <summary>
    /// Get the Radiance lib directory (for RAYPATH, contains .cal files).
    /// </summary>
    public string GetLibDir()
    {
        var binDir = GetBinDir();
        if (binDir != null)
        {
            var libDir = Path.Combine(Path.GetDirectoryName(binDir)!, "lib");
            if (Directory.Exists(libDir))
                return libDir;
        }

        // Check RAYPATH env var directly
        var raypath = Environment.GetEnvironmentVariable("RAYPATH");
        if (!string.IsNullOrEmpty(raypath))
        {
            var sep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            var firstPath = raypath.Split(sep).FirstOrDefault(d => Directory.Exists(d));
            if (firstPath != null)
                return firstPath;
        }

        throw new DirectoryNotFoundException(
            "Could not find Radiance lib directory (needed for .cal files). " +
            "Set --radiance-dir or the RAYPATH environment variable.");
    }

    /// <summary>
    /// Validate that all required binaries are available.
    /// Returns list of missing binary names.
    /// </summary>
    public List<string> ValidateRequiredBinaries()
    {
        var missing = new List<string>();
        foreach (var name in RequiredBinaries)
        {
            try { GetBinaryPath(name); }
            catch (FileNotFoundException) { missing.Add(name); }
        }
        return missing;
    }

    private string? GetBinDir()
    {
        if (_resolvedBinDir != null)
            return _resolvedBinDir;

        _resolvedBinDir = _explicitBinDir
            ?? FindBinDirFromRaypath()
            ?? FindBinDirFromCommonPaths();

        return _resolvedBinDir;
    }

    private static string? FindBinDirFromRaypath()
    {
        var raypath = Environment.GetEnvironmentVariable("RAYPATH");
        if (string.IsNullOrEmpty(raypath))
            return null;

        var sep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var dir in raypath.Split(sep))
        {
            if (!Directory.Exists(dir)) continue;
            // RAYPATH usually points to lib; bin is a sibling
            var binDir = Path.Combine(Path.GetDirectoryName(dir)!, "bin");
            if (Directory.Exists(binDir) && FindExecutable(binDir, "oconv") != null)
                return binDir;
        }
        return null;
    }

    private static string? FindBinDirFromCommonPaths()
    {
        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { @"C:\Radiance\bin", @"C:\Program Files\Radiance\bin" }
            : new[] { "/usr/local/radiance/bin", "/opt/radiance/bin", "/usr/local/bin" };

        return candidates.FirstOrDefault(d =>
            Directory.Exists(d) && FindExecutable(d, "oconv") != null);
    }

    private static string? FindExecutable(string? dir, string name)
    {
        if (dir != null)
        {
            var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            var path = Path.Combine(dir, name + ext);
            if (File.Exists(path))
                return path;
            return null;
        }

        // Fall back to PATH lookup
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var sep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var exeExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        foreach (var pathDir in pathVar.Split(sep))
        {
            var candidate = Path.Combine(pathDir, name + exeExt);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
