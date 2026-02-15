using System.Globalization;
using System.Text;
using System.Text.Json;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.IO;

/// <summary>
/// Writes annual simulation results to CSV or binary format.
/// </summary>
public static class ResultsWriter
{
    public static async Task WriteCsvAsync(AnnualResults results, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, "annual_illuminance.csv");

        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        await writer.WriteLineAsync("# Annual Daylight Results (illuminance in lux)");
        await writer.WriteLineAsync($"# Sensors: {results.SensorCount}, Hours: {results.HourCount}");
        await writer.WriteLineAsync("# Format: sensor_x, sensor_y, sensor_z, hour_1, hour_2, ..., hour_8760");

        for (int i = 0; i < results.SensorCount; i++)
        {
            var sensor = results.Sensors.Points[i];
            var sb = new StringBuilder();
            sb.Append(sensor.X.ToString("G", CultureInfo.InvariantCulture));
            sb.Append(", ");
            sb.Append(sensor.Y.ToString("G", CultureInfo.InvariantCulture));
            sb.Append(", ");
            sb.Append(sensor.Z.ToString("G", CultureInfo.InvariantCulture));

            for (int h = 0; h < results.HourCount; h++)
            {
                sb.Append(", ");
                sb.Append(results.Values[i][h].ToString("F1", CultureInfo.InvariantCulture));
            }

            await writer.WriteLineAsync(sb.ToString());
        }
    }

    public static async Task WriteBinaryAsync(AnnualResults results, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        // Write binary data (float32, row-major: [sensors x hours])
        var binPath = Path.Combine(outputDir, "annual_illuminance.bin");
        using (var stream = File.Create(binPath))
        {
            for (int i = 0; i < results.SensorCount; i++)
            {
                var floats = results.Values[i].Select(v => (float)v).ToArray();
                var bytes = new byte[floats.Length * sizeof(float)];
                Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
                await stream.WriteAsync(bytes);
            }
        }

        // Write JSON sidecar
        var jsonPath = Path.Combine(outputDir, "annual_illuminance.json");
        var metadata = new
        {
            format = "float32",
            shape = new[] { results.SensorCount, results.HourCount },
            units = "lux",
            layout = "row_major_sensors_x_hours",
            sensors = results.Sensors.Points.Select(p => new
            {
                x = p.X, y = p.Y, z = p.Z,
                nx = p.Nx, ny = p.Ny, nz = p.Nz
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(jsonPath, json);
    }
}
