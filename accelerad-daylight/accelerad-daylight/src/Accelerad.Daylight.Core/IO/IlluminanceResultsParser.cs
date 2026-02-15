using System.Globalization;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.IO;

/// <summary>
/// Parses the ASCII illuminance output from rmtxop into AnnualResults.
/// The file contains one row per sensor, with hourly illuminance values
/// separated by tabs. Header lines start with non-numeric characters.
/// </summary>
public static class IlluminanceResultsParser
{
    public static async Task<AnnualResults> ParseAsync(string filePath, SensorGrid sensorGrid)
    {
        var allValues = new List<double[]>();

        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            // Skip header lines (Radiance matrix headers start with non-numeric chars)
            if (!char.IsDigit(trimmed[0]) && trimmed[0] != '-' && trimmed[0] != '.')
                continue;

            var parts = trimmed.Split(new[] { '\t', ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            var hourValues = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                hourValues[i] = double.Parse(parts[i], CultureInfo.InvariantCulture);
            }
            allValues.Add(hourValues);
        }

        if (allValues.Count != sensorGrid.Count)
        {
            throw new InvalidOperationException(
                $"Result row count ({allValues.Count}) does not match sensor count ({sensorGrid.Count}). " +
                "The simulation may have failed or produced unexpected output.");
        }

        return new AnnualResults
        {
            Sensors = sensorGrid,
            Values = allValues.ToArray()
        };
    }
}
