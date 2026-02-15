using Accelerad.Daylight.Core.IO;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Tests;

public class ResultsWriterTests
{
    [Fact]
    public async Task WriteCsvAsync_CreatesReadableFile()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(1, 2, 3, 0, 0, 1));

        var results = new AnnualResults
        {
            Sensors = grid,
            Values = new[] { new double[] { 100.5, 200.3, 50.0 } }
        };

        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await ResultsWriter.WriteCsvAsync(results, outputDir);
            var csvPath = Path.Combine(outputDir, "annual_illuminance.csv");
            Assert.True(File.Exists(csvPath));

            var lines = await File.ReadAllLinesAsync(csvPath);
            // First 3 lines are comments
            Assert.StartsWith("#", lines[0]);
            // Data line
            Assert.Contains("1, 2, 3", lines[3]);
            Assert.Contains("100.5", lines[3]);
        }
        finally { Directory.Delete(outputDir, true); }
    }

    [Fact]
    public async Task WriteBinaryAsync_CreatesFilesWithMetadata()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(1, 2, 3, 0, 0, 1));
        grid.Points.Add(new SensorPoint(4, 5, 6, 0, 0, -1));

        var results = new AnnualResults
        {
            Sensors = grid,
            Values = new[]
            {
                new double[] { 100, 200, 300 },
                new double[] { 400, 500, 600 }
            }
        };

        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await ResultsWriter.WriteBinaryAsync(results, outputDir);

            var binPath = Path.Combine(outputDir, "annual_illuminance.bin");
            var jsonPath = Path.Combine(outputDir, "annual_illuminance.json");
            Assert.True(File.Exists(binPath));
            Assert.True(File.Exists(jsonPath));

            // Binary: 2 sensors × 3 hours × 4 bytes/float = 24 bytes
            var binData = await File.ReadAllBytesAsync(binPath);
            Assert.Equal(24, binData.Length);

            // JSON should contain sensor info and shape
            var json = await File.ReadAllTextAsync(jsonPath);
            Assert.Contains("\"units\": \"lux\"", json);
            Assert.Contains("\"format\": \"float32\"", json);
        }
        finally { Directory.Delete(outputDir, true); }
    }
}
