using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Accelerad.Daylight.Grasshopper;

public static class BrepMesher
{
    public static List<Mesh> MeshBreps(List<Brep> breps, double density)
    {
        var meshes = new List<Mesh>();
        var settings = new MeshingParameters(density);
        
        foreach (var brep in breps)
        {
            var brepMeshes = Mesh.CreateFromBrep(brep, settings);
            if (brepMeshes != null)
            {
                var joined = new Mesh();
                foreach (var m in brepMeshes)
                    joined.Append(m);
                
                if (joined.IsValid)
                    meshes.Add(joined);
            }
        }
        return meshes;
    }
}
