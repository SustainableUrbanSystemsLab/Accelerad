namespace Accelerad.Daylight.Core.Models;

public readonly record struct SensorPoint(
    double X, double Y, double Z,
    double Nx, double Ny, double Nz)
{
    /// <summary>
    /// Format as Radiance sensor input: "x y z nx ny nz"
    /// </summary>
    public string ToRadianceFormat() =>
        $"{X:G} {Y:G} {Z:G} {Nx:G} {Ny:G} {Nz:G}";
}
