namespace Accelerad.Daylight.Core.Models;

public class AnnualResults
{
    /// <summary>Sensor grid used for the simulation.</summary>
    public required SensorGrid Sensors { get; init; }

    /// <summary>
    /// Annual hourly illuminance values (lux).
    /// Indexed as [sensorIndex][hourIndex] where hourIndex is 0-8759.
    /// </summary>
    public required double[][] Values { get; init; }

    /// <summary>Number of sensors.</summary>
    public int SensorCount => Sensors.Count;

    /// <summary>Number of hours (typically 8760).</summary>
    public int HourCount => Values.Length > 0 ? Values[0].Length : 0;

    /// <summary>Annual sum of illuminance for each sensor.</summary>
    public double[] GetAnnualSums()
    {
        var sums = new double[SensorCount];
        for (int i = 0; i < SensorCount; i++)
            sums[i] = Values[i].Sum();
        return sums;
    }
}
