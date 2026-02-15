using Accelerad.Daylight.Core.IO;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Tests;

public class IlluminanceResultsParserTests
{
    [Fact]
    public async Task ParseAsync_ParsesTabDelimitedValues()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(0, 0, 0, 0, 0, 1));
        grid.Points.Add(new SensorPoint(1, 0, 0, 0, 0, 1));

        var tempFile = Path.GetTempFileName();
        try
        {
            // Simulate rmtxop output: header lines + data rows
            await File.WriteAllTextAsync(tempFile,
                "NROWS=2\n" +
                "NCOLS=3\n" +
                "NCOMP=1\n" +
                "FORMAT=ascii\n" +
                "\n" +
                "100.5\t200.3\t0.0\n" +
                "300.1\t400.7\t50.2\n");

            var results = await IlluminanceResultsParser.ParseAsync(tempFile, grid);

            Assert.Equal(2, results.SensorCount);
            Assert.Equal(3, results.HourCount);
            Assert.Equal(100.5, results.Values[0][0]);
            Assert.Equal(200.3, results.Values[0][1]);
            Assert.Equal(0.0, results.Values[0][2]);
            Assert.Equal(300.1, results.Values[1][0]);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnMismatchedSensorCount()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(0, 0, 0, 0, 0, 1));

        var tempFile = Path.GetTempFileName();
        try
        {
            // 2 data rows but only 1 sensor
            await File.WriteAllTextAsync(tempFile,
                "100.5\t200.3\n" +
                "300.1\t400.7\n");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => IlluminanceResultsParser.ParseAsync(tempFile, grid));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public Task AnnualResults_GetAnnualSums()
    {
        var grid = new SensorGrid();
        grid.Points.Add(new SensorPoint(0, 0, 0, 0, 0, 1));

        var results = new AnnualResults
        {
            Sensors = grid,
            Values = new[] { new double[] { 100, 200, 300 } }
        };

        var sums = results.GetAnnualSums();
        Assert.Equal(600, sums[0]);
        return Task.CompletedTask;
    }
}
