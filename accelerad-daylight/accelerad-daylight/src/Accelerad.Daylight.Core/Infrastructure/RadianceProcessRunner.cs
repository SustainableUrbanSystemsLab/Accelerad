using System.Diagnostics;
using System.Text;
using Accelerad.Daylight.Core.Models;

namespace Accelerad.Daylight.Core.Infrastructure;

/// <summary>
/// Runs Radiance executables as subprocesses with proper RAYPATH configuration.
/// Supports stdout capture, stdin piping, and process-to-process piping.
/// </summary>
public class RadianceProcessRunner
{
    private readonly BinaryFinder _binaryFinder;
    private readonly string _libDir;

    public RadianceProcessRunner(BinaryFinder binaryFinder)
    {
        _binaryFinder = binaryFinder;
        _libDir = binaryFinder.GetLibDir();
    }

    /// <summary>
    /// Run a Radiance command and capture stdout/stderr.
    /// </summary>
    public async Task<ProcessResult> RunAsync(
        string binaryName,
        string arguments,
        string? workingDirectory = null,
        string? stdinData = null,
        string? stdinFile = null,
        string? stdoutFile = null,
        TimeSpan? timeout = null)
    {
        var binaryPath = _binaryFinder.GetBinaryPath(binaryName);
        var command = $"{binaryPath} {arguments}";

        var psi = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinData != null || stdinFile != null,
            CreateNoWindow = true,
        };

        // Set RAYPATH environment variable
        psi.Environment["RAYPATH"] = _libDir + (
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows) ? ";" : ":") + ".";

        using var process = new Process { StartInfo = psi };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        FileStream? stdoutStream = null;
        Task? stdoutCopyTask = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderrBuilder.AppendLine(e.Data);
        };

        process.Start();

        // If writing stdout to file, use raw stream copy instead of line-by-line
        if (stdoutFile != null)
        {
            stdoutStream = File.Create(stdoutFile);
            stdoutCopyTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutStream);
        }
        else
        {
            process.BeginOutputReadLine();
        }

        process.BeginErrorReadLine();

        // Write stdin if provided
        if (stdinData != null)
        {
            await process.StandardInput.WriteAsync(stdinData);
            process.StandardInput.Close();
        }
        else if (stdinFile != null)
        {
            using var reader = File.OpenRead(stdinFile);
            await reader.CopyToAsync(process.StandardInput.BaseStream);
            process.StandardInput.Close();
        }

        // Wait for stdout copy to complete before checking exit (prevents truncation)
        if (stdoutCopyTask != null)
            await stdoutCopyTask;

        var timeoutMs = (int)(timeout ?? TimeSpan.FromMinutes(30)).TotalMilliseconds;
        var exited = await Task.Run(() => process.WaitForExit(timeoutMs));

        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Command timed out after {timeoutMs / 1000}s: {command}");
        }

        if (stdoutStream != null)
        {
            await stdoutStream.FlushAsync();
            stdoutStream.Dispose();
        }

        return new ProcessResult(
            process.ExitCode,
            stdoutBuilder.ToString(),
            stderrBuilder.ToString(),
            command);
    }

    /// <summary>
    /// Run a Radiance command, piping stdout to a file. Throws on failure.
    /// </summary>
    public async Task RunToFileAsync(
        string binaryName,
        string arguments,
        string outputFile,
        string? workingDirectory = null,
        string? stdinFile = null)
    {
        var result = await RunAsync(
            binaryName, arguments,
            workingDirectory: workingDirectory,
            stdinFile: stdinFile,
            stdoutFile: outputFile);
        result.ThrowIfFailed();
    }

    /// <summary>
    /// Run a pipeline of commands, piping stdout of each to stdin of the next.
    /// Final stdout goes to outputFile.
    /// </summary>
    public async Task RunPipelineAsync(
        (string binaryName, string arguments)[] commands,
        string outputFile,
        string? workingDirectory = null)
    {
        var processes = new List<Process>();
        var wd = workingDirectory ?? Directory.GetCurrentDirectory();

        try
        {
            for (int i = 0; i < commands.Length; i++)
            {
                var (name, args) = commands[i];
                var binaryPath = _binaryFinder.GetBinaryPath(name);

                var psi = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = args,
                    WorkingDirectory = wd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = i > 0,
                    CreateNoWindow = true,
                };
                psi.Environment["RAYPATH"] = _libDir + (
                    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Windows) ? ";" : ":") + ".";

                var process = new Process { StartInfo = psi };
                process.Start();
                processes.Add(process);

                // Pipe previous stdout to current stdin
                if (i > 0)
                {
                    var prev = processes[i - 1];
                    _ = prev.StandardOutput.BaseStream.CopyToAsync(process.StandardInput.BaseStream)
                        .ContinueWith(_ => process.StandardInput.Close());
                }
            }

            // Write final stdout to file
            var last = processes[^1];
            using var outFile = File.Create(outputFile);
            await last.StandardOutput.BaseStream.CopyToAsync(outFile);

            // Wait for all processes
            foreach (var p in processes)
            {
                await Task.Run(() => p.WaitForExit(300_000));
                if (p.ExitCode != 0)
                {
                    var stderr = await p.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException(
                        $"Pipeline stage failed (exit {p.ExitCode}): " +
                        $"{p.StartInfo.FileName} {p.StartInfo.Arguments}\n{stderr}");
                }
            }
        }
        finally
        {
            foreach (var p in processes)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
                p.Dispose();
            }
        }
    }
}
