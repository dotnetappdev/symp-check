using System.Diagnostics;
using DevBootstrapper.Models;

namespace DevBootstrapper.Services;

/// <summary>Invokes winget package installation commands.</summary>
public sealed class PackageProviderService
{
    /// <summary>
    /// Resolves the effective provider.
    /// The installer is winget-only.
    /// </summary>
    public PackageProvider Resolve() => PackageProvider.Winget;

    /// <summary>Installs a package with the specified provider and package ID.</summary>
    public async Task<bool> InstallPackageAsync(
        PackageProvider provider,
        string packageId,
        string? extraArgs,
        Action<string> log,
        bool silentInstall = false)
    {
        string args =
            $"install --id {packageId} --silent --accept-package-agreements --accept-source-agreements{(extraArgs is not null ? " " + extraArgs : "")}";
        const string exe = "winget";

        return await RunProcessAsync(exe, args, log);
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

    private static async Task<bool> RunProcessAsync(string exe, string arguments, Action<string> log)
    {
        try
        {
            log($"Running: {exe} {arguments}");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
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
