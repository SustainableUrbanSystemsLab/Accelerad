using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rhino.Geometry;

namespace Accelerad.Daylight.Grasshopper;

public static class ObjExporter
{
    public static void Export(List<Mesh> meshes, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("# Accelerad Daylight OBJ Export");
        
        int vertexOffset = 1;
        int normalOffset = 1;
        
        for (int i = 0; i < meshes.Count; i++)
        {
            var mesh = meshes[i];
            if (mesh == null || !mesh.IsValid || mesh.Faces.Count == 0)
                continue;

            if (mesh.Normals.Count != mesh.Vertices.Count)
                mesh.Normals.ComputeNormals();
            var hasVertexNormals = mesh.Normals.Count == mesh.Vertices.Count;

            writer.WriteLine($"g mesh_{i}");
            
            // Vertices
            foreach (var v in mesh.Vertices)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z));
            }
            
            // Normals
            if (hasVertexNormals)
            {
                foreach (var n in mesh.Normals)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", n.X, n.Y, n.Z));
                }
            }
            
            // Faces
            foreach (var face in mesh.Faces)
            {
                // OBJ indices are 1-based.
                var a = face.A + vertexOffset;
                var b = face.B + vertexOffset;
                var c = face.C + vertexOffset;
                var d = face.D + vertexOffset;

                if (face.IsQuad)
                {
                    if (hasVertexNormals)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "f {0}//{4} {1}//{5} {2}//{6} {3}//{7}",
                            a, b, c, d,
                            face.A + normalOffset, face.B + normalOffset, face.C + normalOffset, face.D + normalOffset));
                    }
                    else
                    {
                        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "f {0} {1} {2} {3}", a, b, c, d));
                    }
                }
                else
                {
                    if (hasVertexNormals)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "f {0}//{3} {1}//{4} {2}//{5}",
                            a, b, c,
                            face.A + normalOffset, face.B + normalOffset, face.C + normalOffset));
                    }
                    else
                    {
                        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "f {0} {1} {2}", a, b, c));
                    }
                }
            }
            
            vertexOffset += mesh.Vertices.Count;
            if (hasVertexNormals)
                normalOffset += mesh.Normals.Count;
        }
    }
}
