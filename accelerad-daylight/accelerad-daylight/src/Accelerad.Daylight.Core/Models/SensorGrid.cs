namespace Accelerad.Daylight.Core.Models;

public class SensorGrid
{
    public List<SensorPoint> Points { get; } = new();

    public int Count => Points.Count;

    /// <summary>
    /// Write all sensor points to a file in Radiance format (one per line).
    /// </summary>
    public async Task WriteToFileAsync(string filePath)
    {
        var lines = Points.Select(p => p.ToRadianceFormat());
        await File.WriteAllLinesAsync(filePath, lines);
    }
}
