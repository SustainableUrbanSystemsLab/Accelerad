namespace Accelerad.Daylight.Core.Infrastructure;

/// <summary>
/// Manages a temporary working directory for intermediate simulation files.
/// Supports cleanup on completion or retention for debugging.
/// </summary>
public class TempDirectoryManager : IDisposable
{
    public string TempDir { get; }
    private readonly bool _keepOnDispose;
    private bool _disposed;

    public TempDirectoryManager(bool keepOnDispose = false, string? basePath = null)
    {
        _keepOnDispose = keepOnDispose;
        var baseDir = basePath ?? Path.GetTempPath();
        TempDir = Path.Combine(baseDir, $"accelerad-daylight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempDir);
    }

    /// <summary>Get a path for a named intermediate file within the temp directory.</summary>
    public string GetPath(string fileName) => Path.Combine(TempDir, fileName);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_keepOnDispose)
        {
            try { Directory.Delete(TempDir, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }
}
