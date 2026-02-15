using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rhino.Geometry;

namespace Accelerad.Daylight.Grasshopper;

public record struct SensorPoint(Point3d Location, Vector3d Normal);

public static class SensorGridBuilder
{
    public static List<Mesh> PrepareMeshes(List<Mesh> meshes)
    {
        var prepared = new List<Mesh>();
        foreach (var mesh in meshes)
        {
            if (mesh == null || !mesh.IsValid || mesh.Faces.Count == 0)
                continue;

            var analysisMesh = mesh.DuplicateMesh();
            analysisMesh.Vertices.CombineIdentical(true, true);
            analysisMesh.Weld(Math.PI);
            analysisMesh.UnifyNormals();
            analysisMesh.Normals.ComputeNormals();
            analysisMesh.FaceNormals.ComputeFaceNormals();
            prepared.Add(analysisMesh);
        }
        return prepared;
    }

    public static List<SensorPoint> Build(List<Mesh> meshes)
    {
        return BuildFromPrepared(PrepareMeshes(meshes));
    }

    public static List<SensorPoint> BuildFromPrepared(List<Mesh> preparedMeshes)
    {
        var sensors = new List<SensorPoint>();
        foreach (var analysisMesh in preparedMeshes)
        {
            for (int i = 0; i < analysisMesh.Faces.Count; i++)
            {
                var pt = analysisMesh.Faces.GetFaceCenter(i);
                var normal = (Vector3d)analysisMesh.FaceNormals[i];
                if (!normal.Unitize())
                    continue;

                // Offset sensor slightly to avoid self-intersection
                var offsetPt = pt + normal * 0.001; // 1mm offset
                
                sensors.Add(new SensorPoint(offsetPt, normal));
            }
        }
        return sensors;
    }
    
    public static void WriteCsv(List<SensorPoint> sensors, string path)
    {
        using var writer = new StreamWriter(path);
        // Header? No, sensors.csv for Accelerad usually just x,y,z,nx,ny,nz
        // Cli expects standard CSV ? 
        // SensorGridReader.cs in Core:
        // Reads lines. Values separated by comma or space.
        // x, y, z, nx, ny, nz
        
        foreach (var s in sensors)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, 
                "{0},{1},{2},{3},{4},{5}", 
                s.Location.X, s.Location.Y, s.Location.Z, 
                s.Normal.X, s.Normal.Y, s.Normal.Z));
        }
    }
}
