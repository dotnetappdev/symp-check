using System.Diagnostics;
using System.Runtime.InteropServices;
using DevBootstrapper.Models;

namespace DevBootstrapper.Services;

/// <summary>Detects and invokes package providers (winget / Chocolatey).</summary>
public sealed class PackageProviderService
{
    public bool IsWingetAvailable() => IsCommandAvailable("winget");

    public bool IsChocolateyAvailable() => IsCommandAvailable("choco");

    /// <summary>
    /// Resolves the effective provider based on user choice.
    /// Falls back to the next available provider when AutoDetect is selected.
    /// </summary>
    public PackageProvider Resolve(PackageProvider requested)
    {
        if (requested == PackageProvider.AutoDetect)
        {
            if (IsWingetAvailable()) return PackageProvider.Winget;
            if (IsChocolateyAvailable()) return PackageProvider.Chocolatey;
            return PackageProvider.Winget; // will be installed later
        }

        return requested;
    }

    /// <summary>Installs Chocolatey via the official PowerShell bootstrap script.</summary>
    public async Task<bool> InstallChocolateyAsync(Action<string> log)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            log("Chocolatey installation is only supported on Windows.");
            return false;
        }

        log("Downloading and installing Chocolatey...");
        const string script =
            "Set-ExecutionPolicy Bypass -Scope Process -Force;" +
            "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072;" +
            "iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))";

        return await RunProcessAsync("powershell.exe", $"-NoProfile -Command \"{script}\"", log);
    }

    /// <summary>Installs a package with the specified provider and package ID.</summary>
    public async Task<bool> InstallPackageAsync(
        PackageProvider provider,
        string packageId,
        string? extraArgs,
        Action<string> log,
        bool silentInstall = false)
    {
        string args = provider switch
        {
            PackageProvider.Winget =>
                $"install --id {packageId} --silent --accept-package-agreements --accept-source-agreements{(extraArgs is not null ? " " + extraArgs : "")}",
            PackageProvider.Chocolatey =>
                $"install {packageId} -y{(silentInstall ? " --no-progress" : "")}{(extraArgs is not null ? " " + extraArgs : "")}",
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };

        string exe = provider switch
        {
            PackageProvider.Winget => "winget",
            PackageProvider.Chocolatey => "choco",
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };

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
