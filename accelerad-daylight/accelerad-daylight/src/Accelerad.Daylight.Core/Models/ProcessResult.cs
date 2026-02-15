namespace Accelerad.Daylight.Core.Models;

public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Command)
{
    public bool Success => ExitCode == 0;

    public void ThrowIfFailed()
    {
        if (!Success)
        {
            throw new InvalidOperationException(
                $"Command failed with exit code {ExitCode}: {Command}\n" +
                $"stderr: {StandardError}");
        }
    }
}
