using Accelerad.Daylight.Core.Infrastructure;

namespace Accelerad.Daylight.Core.Tests;

public class TempDirectoryManagerTests
{
    [Fact]
    public void CreatesDirectoryOnConstruction()
    {
        using var mgr = new TempDirectoryManager();
        Assert.True(Directory.Exists(mgr.TempDir));
    }

    [Fact]
    public void CleansUpOnDispose()
    {
        string tempDir;
        using (var mgr = new TempDirectoryManager())
        {
            tempDir = mgr.TempDir;
            File.WriteAllText(mgr.GetPath("test.txt"), "hello");
            Assert.True(File.Exists(mgr.GetPath("test.txt")));
        }
        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void KeepsFilesWhenKeepOnDispose()
    {
        string tempDir;
        using (var mgr = new TempDirectoryManager(keepOnDispose: true))
        {
            tempDir = mgr.TempDir;
            File.WriteAllText(mgr.GetPath("test.txt"), "hello");
        }
        Assert.True(Directory.Exists(tempDir));
        // Clean up manually
        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void GetPath_ReturnsPathInTempDir()
    {
        using var mgr = new TempDirectoryManager();
        var path = mgr.GetPath("scene.oct");
        Assert.Equal(Path.Combine(mgr.TempDir, "scene.oct"), path);
    }
}
