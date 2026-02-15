using Accelerad.Daylight.Core.IO;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Tests;

public class SensorGridReaderTests
{
    [Fact]
    public async Task ReadAsync_ParsesCsvCorrectly()
    {
        var path = GetTestDataPath("sensors.csv");
        var grid = await SensorGridReader.ReadAsync(path);

        Assert.Equal(6, grid.Count);
        Assert.Equal(1.0, grid.Points[0].X);
        Assert.Equal(1.0, grid.Points[0].Y);
        Assert.Equal(0.8, grid.Points[0].Z);
        Assert.Equal(0, grid.Points[0].Nx);
        Assert.Equal(0, grid.Points[0].Ny);
        Assert.Equal(1, grid.Points[0].Nz);
    }

    [Fact]
    public async Task ReadAsync_SkipsCommentLines()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile,
                "# comment\n1 2 3 0 0 1\n\n# another comment\n4 5 6 0 0 -1\n");
            var grid = await SensorGridReader.ReadAsync(tempFile);
            Assert.Equal(2, grid.Count);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task ReadAsync_SupportsSpaceDelimited()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "1.5 2.5 3.5 0 0 1\n");
            var grid = await SensorGridReader.ReadAsync(tempFile);
            Assert.Equal(1, grid.Count);
            Assert.Equal(1.5, grid.Points[0].X);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task ReadAsync_ThrowsOnInvalidData()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "1 2 3\n"); // only 3 values
            await Assert.ThrowsAsync<FormatException>(
                () => SensorGridReader.ReadAsync(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task ReadAsync_ThrowsOnEmptyFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "# only comments\n");
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SensorGridReader.ReadAsync(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void SensorPoint_ToRadianceFormat()
    {
        var point = new SensorPoint(1.5, 2.5, 3.5, 0, 0, 1);
        Assert.Equal("1.5 2.5 3.5 0 0 1", point.ToRadianceFormat());
    }

    [Fact]
    public async Task SensorGrid_WriteToFileAsync()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(1, 2, 3, 0, 0, 1));
        grid.Points.Add(new SensorPoint(4, 5, 6, 0, 0, -1));

        var tempFile = Path.GetTempFileName();
        try
        {
            await grid.WriteToFileAsync(tempFile);
            var lines = await File.ReadAllLinesAsync(tempFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal("1 2 3 0 0 1", lines[0]);
            Assert.Equal("4 5 6 0 0 -1", lines[1]);
        }
        finally { File.Delete(tempFile); }
    }

    private static string GetTestDataPath(string fileName)
    {
        // Walk up from bin/Debug/net8.0 to find TestData
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "TestData")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "TestData", fileName);
    }
}
