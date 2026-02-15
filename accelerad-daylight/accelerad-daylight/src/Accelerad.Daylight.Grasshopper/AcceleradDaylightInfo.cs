using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Accelerad.Daylight.Grasshopper;

public class AcceleradDaylightInfo : GH_AssemblyInfo
{
    public override string Name => "Accelerad Daylight";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap? Icon => null;

    //Return a short string describing the library.
    public override string Description => "Grasshopper plugin to run Accelerad Daylight simulations via CLI.";

    public override Guid Id => new Guid("DA734B85-B44C-CA6A-0B3C-6126DA734B85");

    public override string AuthorName => "Sustainable Urban Systems Lab";

    public override string AuthorContact => "https://github.com/SustainLab/Accelerad";
}
