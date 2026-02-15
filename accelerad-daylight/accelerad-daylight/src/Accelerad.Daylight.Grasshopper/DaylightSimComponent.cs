using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace Accelerad.Daylight.Grasshopper;

public class DaylightSimComponent : GH_Component
{
    private enum SimState { Idle, Running, Completed, Error }
    private SimState _state = SimState.Idle;
    private string _progressText = "Idle";
    
    // Cached results
    private GH_Structure<GH_Number>? _cachedIlluminance;
    private List<double[]>? _cachedHourlyValues;
    private List<Point3d>? _cachedSensors;
    private List<Vector3d>? _cachedNormals;
    private List<Mesh>? _cachedAnalysisMeshes;
    private string? _cachedResultPath;
    private string? _lastSimulationSignature;
    private string? _errorMessage;

    public DaylightSimComponent()
      : base("Daylight Simulation", "Daylight",
          "Run annual daylight simulation using Accelerad CLI (Async)",
          "Accelerad", "Simulation")
    {
    }

    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
        pManager.AddGeometryParameter("Geometry", "G", "Input Geometry (Mesh or Brep)", GH_ParamAccess.list);
        pManager.AddTextParameter("EPW File", "EPW", "Path to EPW weather file", GH_ParamAccess.item);
        // Removed Mesh Density as per request
        pManager.AddTextParameter("Radiance Directory", "RAD", "Path to Radiance binaries (default: /usr/local/radiance/bin)", GH_ParamAccess.item, "/usr/local/radiance/bin");
        pManager.AddIntegerParameter("Sky Subdivision", "MF", "Reinhart sky subdivision factor (4=fast, 6-8=less patch noise)", GH_ParamAccess.item, 6);
        pManager.AddIntegerParameter("Ambient Divisions", "AD", "Ambient divisions for sampling quality (higher reduces noise)", GH_ParamAccess.item, 16384);
        pManager.AddIntegerParameter("Ambient Bounces", "AB", "Ambient bounces", GH_ParamAccess.item, 5);
        pManager.AddIntegerParameter("Hour", "H", "Hour index to extract for visualization (0-8759)", GH_ParamAccess.item, 12);
        pManager.AddBooleanParameter("Run", "Run", "Set to true to run simulation", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
        pManager.AddNumberParameter("Illuminance", "Lux", "Annual illuminance values per sensor (8760 hours)", GH_ParamAccess.tree);
        pManager.AddPointParameter("Sensors", "Pts", "Sensor points", GH_ParamAccess.list);
        pManager.AddVectorParameter("Normals", "Vec", "Sensor normals", GH_ParamAccess.list);
        pManager.AddTextParameter("Result Path", "Path", "Path to results directory", GH_ParamAccess.item);
        pManager.AddNumberParameter("Hour Illuminance", "Lux@H", "Illuminance at selected hour (one value per sensor)", GH_ParamAccess.list);
        pManager.AddMeshParameter("Analysis Mesh", "M", "Prepared mesh used for simulation", GH_ParamAccess.list);
        pManager.AddMeshParameter("Colored Mesh", "CM", "Per-face colored mesh for selected hour", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        bool run = false;
        DA.GetData("Run", ref run);

        int hourIndex = 12;
        int mf = 6;
        int ad = 16384;
        int ab = 5;
        DA.GetData("Sky Subdivision", ref mf);
        DA.GetData("Ambient Divisions", ref ad);
        DA.GetData("Ambient Bounces", ref ab);
        DA.GetData("Hour", ref hourIndex);
        mf = Math.Max(1, Math.Min(12, mf));
        ad = Math.Max(128, ad);
        ab = Math.Max(0, ab);
        hourIndex = Math.Max(0, Math.Min(8759, hourIndex));

        if (!run)
        {
            _state = SimState.Idle;
            _cachedIlluminance = null;
            _cachedHourlyValues = null;
            _cachedSensors = null;
            _cachedNormals = null;
            _cachedAnalysisMeshes = null;
            _cachedResultPath = null;
            _lastSimulationSignature = null;
            _errorMessage = null;
            _progressText = "Idle";
            Message = "Idle";
            return;
        }

        // Gather inputs whenever Run is true so we can detect changes automatically.
        var geometryList = new List<IGH_GeometricGoo>();
        string epwPath = "";
        double meshDensity = 0.5; // Default fallback
        string radDir = "/usr/local/radiance/bin";

        if (!DA.GetDataList("Geometry", geometryList)) return;
        if (!DA.GetData("EPW File", ref epwPath)) return;
        DA.GetData("Radiance Directory", ref radDir);

        // Validation
        if (!File.Exists(epwPath))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "EPW file not found");
            return;
        }

        var simulationSignature = BuildSimulationSignature(geometryList, epwPath, radDir, mf, ad, ab);
        var hasCache = _cachedIlluminance != null &&
                       _cachedHourlyValues != null &&
                       _cachedSensors != null &&
                       _cachedNormals != null &&
                       _cachedAnalysisMeshes != null &&
                       _cachedResultPath != null;
        var inputsChanged = !string.Equals(_lastSimulationSignature, simulationSignature, StringComparison.Ordinal);

        if (_state == SimState.Running)
        {
            Message = _progressText;
            return; // Keep waiting for current async run to complete
        }

        if (!hasCache || inputsChanged || _state == SimState.Error)
        {
            // Start or restart simulation automatically whenever relevant inputs change.
            _state = SimState.Running;
            _progressText = "Starting...";
            _errorMessage = null;
            Message = "Starting...";

            var geoCopy = geometryList.ToList();
            Task.Run(() => RunSimulation(geoCopy, epwPath, meshDensity, radDir, simulationSignature, mf, ad, ab));
            return;
        }

        _state = SimState.Completed;
        Message = "Done";
        if (_cachedIlluminance != null) DA.SetDataTree(0, _cachedIlluminance);
        if (_cachedSensors != null) DA.SetDataList(1, _cachedSensors);
        if (_cachedNormals != null) DA.SetDataList(2, _cachedNormals);
        if (_cachedResultPath != null) DA.SetData(3, _cachedResultPath);
        DA.SetDataList(4, BuildHourValues(hourIndex));
        if (_cachedAnalysisMeshes != null) DA.SetDataList(5, _cachedAnalysisMeshes);
        DA.SetDataList(6, BuildColoredMeshes(hourIndex));
    }

    private List<double> BuildHourValues(int hourIndex)
    {
        var output = new List<double>();
        if (_cachedHourlyValues == null)
            return output;

        foreach (var sensorValues in _cachedHourlyValues)
        {
            if (sensorValues.Length == 0)
            {
                output.Add(0.0);
                continue;
            }
            var idx = Math.Max(0, Math.Min(sensorValues.Length - 1, hourIndex));
            output.Add(sensorValues[idx]);
        }
        return output;
    }

    private List<Mesh> BuildColoredMeshes(int hourIndex)
    {
        var result = new List<Mesh>();
        if (_cachedAnalysisMeshes == null || _cachedHourlyValues == null)
            return result;

        var hourValues = BuildHourValues(hourIndex);
        if (hourValues.Count == 0)
            return result;

        var min = hourValues.Min();
        var max = hourValues.Max();
        var span = Math.Max(1e-9, max - min);

        var sensorOffset = 0;
        foreach (var sourceMesh in _cachedAnalysisMeshes)
        {
            if (sourceMesh == null || !sourceMesh.IsValid || sourceMesh.Faces.Count == 0)
                continue;

            var colored = new Mesh();
            for (int fi = 0; fi < sourceMesh.Faces.Count; fi++)
            {
                if (sensorOffset + fi >= hourValues.Count)
                    break;

                var value = hourValues[sensorOffset + fi];
                var t = (value - min) / span;
                var c = ColorFromUnit(t);

                var face = sourceMesh.Faces[fi];
                var vA = sourceMesh.Vertices[face.A];
                var vB = sourceMesh.Vertices[face.B];
                var vC = sourceMesh.Vertices[face.C];
                var start = colored.Vertices.Count;

                colored.Vertices.Add(vA);
                colored.Vertices.Add(vB);
                colored.Vertices.Add(vC);

                if (face.IsQuad)
                {
                    var vD = sourceMesh.Vertices[face.D];
                    colored.Vertices.Add(vD);
                    colored.Faces.AddFace(start, start + 1, start + 2, start + 3);
                    colored.VertexColors.Add(c);
                    colored.VertexColors.Add(c);
                    colored.VertexColors.Add(c);
                    colored.VertexColors.Add(c);
                }
                else
                {
                    colored.Faces.AddFace(start, start + 1, start + 2);
                    colored.VertexColors.Add(c);
                    colored.VertexColors.Add(c);
                    colored.VertexColors.Add(c);
                }
            }

            colored.Normals.ComputeNormals();
            colored.Compact();
            result.Add(colored);
            sensorOffset += sourceMesh.Faces.Count;
        }

        return result;
    }

    private static Color ColorFromUnit(double t)
    {
        t = Math.Max(0.0, Math.Min(1.0, t));

        // Compact jet-like palette for quick diagnostics.
        var r = Clamp01(1.5 - Math.Abs(4 * t - 3));
        var g = Clamp01(1.5 - Math.Abs(4 * t - 2));
        var b = Clamp01(1.5 - Math.Abs(4 * t - 1));

        return Color.FromArgb(
            (int)Math.Round(255 * r),
            (int)Math.Round(255 * g),
            (int)Math.Round(255 * b));
    }

    private static double Clamp01(double x) => Math.Max(0.0, Math.Min(1.0, x));

    private static string BuildSimulationSignature(
        List<IGH_GeometricGoo> geometryList,
        string epwPath,
        string radDir,
        int mf,
        int ad,
        int ab)
    {
        var sb = new StringBuilder();
        var epwFull = Path.GetFullPath(epwPath);
        var epwInfo = new FileInfo(epwFull);
        sb.Append("epw=").Append(epwFull).Append('|');
        sb.Append("epw_size=").Append(epwInfo.Length).Append('|');
        sb.Append("epw_mtime=").Append(epwInfo.LastWriteTimeUtc.Ticks).Append('|');
        sb.Append("rad=").Append(radDir).Append('|');
        sb.Append("mf=").Append(mf).Append('|');
        sb.Append("ad=").Append(ad).Append('|');
        sb.Append("ab=").Append(ab).Append('|');

        foreach (var geo in geometryList)
        {
            switch (geo)
            {
                case GH_Mesh ghMesh when ghMesh.Value != null:
                {
                    var m = ghMesh.Value;
                    var bb = m.GetBoundingBox(true);
                    sb.Append("mesh:")
                      .Append(m.Vertices.Count).Append(',')
                      .Append(m.Faces.Count).Append(',')
                      .Append(bb.Min.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Min.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Min.Z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    break;
                }
                case GH_Brep ghBrep when ghBrep.Value != null:
                {
                    var b = ghBrep.Value;
                    var bb = b.GetBoundingBox(true);
                    sb.Append("brep:")
                      .Append(b.Faces.Count).Append(',')
                      .Append(b.Edges.Count).Append(',')
                      .Append(bb.Min.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Min.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Min.Z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(bb.Max.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    break;
                }
                default:
                    sb.Append("other:").Append(geo?.TypeName ?? "null").Append('|');
                    break;
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    private void RunSimulation(
        List<IGH_GeometricGoo> geometryList,
        string epwPath,
        double meshDensity,
        string radDir,
        string simulationSignature,
        int mf,
        int ad,
        int ab)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Accelerad_GH_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            UpdateProgress("Processing Geometry...");

            var objPath = Path.Combine(tempDir, "scene.obj");
            var sensorsPath = Path.Combine(tempDir, "sensors.csv");
            var resultsDir = Path.Combine(tempDir, "results");

            // Geometry Processing logic
            var meshes = new List<Mesh>();
            var brepsToMesh = new List<Brep>();

            foreach (var geo in geometryList)
            {
                if (geo is GH_Mesh ghMesh && ghMesh.Value != null) meshes.Add(ghMesh.Value);
                else if (geo is GH_Brep ghBrep && ghBrep.Value != null) brepsToMesh.Add(ghBrep.Value);
            }

            if (brepsToMesh.Count > 0)
            {
                meshes.AddRange(BrepMesher.MeshBreps(brepsToMesh, meshDensity));
            }

            if (meshes.Count == 0) throw new Exception("No valid geometry input.");

            var preparedMeshes = SensorGridBuilder.PrepareMeshes(meshes);
            if (preparedMeshes.Count == 0)
                throw new Exception("No valid mesh faces available after preprocessing.");

            ObjExporter.Export(preparedMeshes, objPath);
            var sensors = SensorGridBuilder.BuildFromPrepared(preparedMeshes);
            SensorGridBuilder.WriteCsv(sensors, sensorsPath);

            UpdateProgress("Launching CLI...");

            // Run CLI
            var projectRoot = "/Users/patrickkastner/Documents/GitHub/SustainLab/Accelerad/accelerad-daylight/accelerad-daylight";
            var args = $"run --project \"{Path.Combine(projectRoot, "src/Accelerad.Daylight.Cli/Accelerad.Daylight.Cli.csproj")}\" -- " +
                       $"run --obj \"{objPath}\" --epw \"{epwPath}\" --sensors \"{sensorsPath}\" --output \"{resultsDir}\" " +
                       $"--radiance-dir \"{radDir}\" --mf {mf} --ad {ad} --ab {ab} --keep-temp";

            var dotnetPath = "/usr/local/share/dotnet/dotnet";
            if (!File.Exists(dotnetPath)) dotnetPath = "dotnet";

            var psi = new ProcessStartInfo(dotnetPath, args)
            {
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            
            process.OutputDataReceived += (s, e) => { if (e.Data != null) HandleCliOutput(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) HandleCliOutput(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception("CLI Execution failed. See output.");
            }

            UpdateProgress("Parsing Results...");

            // Parse Results
            var resultTree = new GH_Structure<GH_Number>();
            var resultsPath = Path.Combine(resultsDir, "annual_illuminance.csv");
            
            if (File.Exists(resultsPath))
            {
                var lines = File.ReadAllLines(resultsPath);
                var hourlyValues = new List<double[]>();
                int sensorIndex = 0;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.TrimStart('\uFEFF').Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length <= 3)
                        continue;

                    var path = new GH_Path(sensorIndex);
                    // CSV rows are: x, y, z, hour_1, ..., hour_8760
                    var sensorHourValues = new List<double>(parts.Length - 3);
                    for (int i = 3; i < parts.Length; i++)
                    {
                        if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        {
                            resultTree.Append(new GH_Number(val), path);
                            sensorHourValues.Add(val);
                        }
                    }
                    hourlyValues.Add(sensorHourValues.ToArray());
                    sensorIndex++;
                }

                if (sensorIndex != sensors.Count)
                {
                    throw new Exception(
                        $"Parsed {sensorIndex} result rows, but generated {sensors.Count} sensors.");
                }

                _cachedIlluminance = resultTree;
                _cachedHourlyValues = hourlyValues;
                _cachedSensors = sensors.Select(s => s.Location).ToList();
                _cachedNormals = sensors.Select(s => s.Normal).ToList();
                _cachedAnalysisMeshes = preparedMeshes.Select(m => m.DuplicateMesh()).ToList();
                _cachedResultPath = resultsDir;
                _lastSimulationSignature = simulationSignature;
                
                _state = SimState.Completed;
            }
            else
            {
                throw new Exception("Results file parsing failed.");
            }
        }
        catch (Exception ex)
        {
            _state = SimState.Error;
            _errorMessage = ex.Message;
        }
        finally
        {
            // Trigger solution update on UI thread
            RhinoApp.InvokeOnUiThread((Action)delegate { ExpireSolution(true); });
        }
    }

    private void HandleCliOutput(string line)
    {
        // Simple heuristic to show progress
        if (line.Contains("[")) // CLI timestamped messages
        {
             // Extract label
             // [10:00:00] Building octree...
             var msg = line;
             int idx = line.IndexOf("]");
             if (idx >= 0 && idx < line.Length - 2) msg = line.Substring(idx + 1).Trim();
             
             UpdateProgress(msg);
        }
    }

    private void UpdateProgress(string msg)
    {
        _progressText = msg;
        // Schedule redraw to show text immediately?
        // Too frequent redraws lag UI.
        // We rely on 'RunSimulation' calling ExpireSolution at end strictly? 
        // User wants to SEE progress.
        // So we should schedule redraw occasionally.
        RhinoApp.InvokeOnUiThread((Action)delegate { 
            Message = msg;
            OnDisplayExpired(true); // Redraws component
        });
    }

    public override Guid ComponentGuid => new Guid("4D0FD91D-5932-445D-A761-ED30EEAB9715");
}
