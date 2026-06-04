using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevBootstrapper.Services;

/// <summary>Handles Git operations: detection and repository cloning.</summary>
public sealed class GitService
{
    public bool IsGitInstalled() => IsCommandAvailable("git");

    /// <summary>Clones a repository into the specified parent directory.</summary>
    public async Task<bool> CloneAsync(string repositoryUrl, string parentDirectory, Action<string> log)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Allow running on Linux/macOS in development
        }

        Directory.CreateDirectory(parentDirectory);

        log($"Cloning {repositoryUrl} into {parentDirectory}...");

        return await RunProcessAsync("git", $"clone \"{repositoryUrl}\"", parentDirectory, log);
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunProcessAsync(
        string exe,
        string arguments,
        string workingDirectory,
        Action<string> log)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) log(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log($"[ERR] {e.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            log($"Error running {exe}: {ex.Message}");
            return false;
        }
    }
}
