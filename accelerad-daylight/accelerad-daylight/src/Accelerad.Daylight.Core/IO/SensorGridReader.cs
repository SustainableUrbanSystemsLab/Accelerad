using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.IO;

/// <summary>
/// Reads sensor grid from a CSV file.
/// Expected format: x,y,z,nx,ny,nz (one sensor per line).
/// Lines starting with # are treated as comments.
/// </summary>
public static class SensorGridReader
{
    public static async Task<SensorGrid> ReadAsync(string filePath)
    {
        var grid = new SensorGrid();
        var lineNumber = 0;

        await foreach (var line in ReadLinesAsync(filePath))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split(new[] { ',', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 6)
            {
                throw new FormatException(
                    $"Sensor grid line {lineNumber}: expected 6 values (x,y,z,nx,ny,nz), got {parts.Length}");
            }

            grid.Points.Add(new SensorPoint(
                double.Parse(parts[0]),
                double.Parse(parts[1]),
                double.Parse(parts[2]),
                double.Parse(parts[3]),
                double.Parse(parts[4]),
                double.Parse(parts[5])));
        }

        if (grid.Count == 0)
            throw new InvalidOperationException($"Sensor grid file is empty: {filePath}");

        return grid;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string filePath)
    {
        using var reader = new StreamReader(filePath);
        while (await reader.ReadLineAsync() is { } line)
            yield return line;
    }
}
