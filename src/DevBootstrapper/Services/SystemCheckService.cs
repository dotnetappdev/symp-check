using System.Runtime.InteropServices;
using DevBootstrapper.Models;

namespace DevBootstrapper.Services;

/// <summary>Checks whether required tools and frameworks are already present.</summary>
public sealed class SystemCheckService
{
    /// <summary>Returns true when .NET Framework 4.8 is installed (Windows only).</summary>
    public bool IsDotNetFramework48Installed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        // Registry key populated by the .NET Framework 4.8 installer
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if (key?.GetValue("Release") is int release)
                return release >= 528040; // 528040 = .NET Framework 4.8 on Windows 10
        }
        catch
        {
            // ignore registry access failures
        }

        return false;
    }

    /// <summary>Returns true when Visual Studio 2022 (any edition) is installed.</summary>
    public bool IsVisualStudio2022Installed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        string[] vsPaths =
        [
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe"
        ];

        return vsPaths.Any(File.Exists);
    }

    /// <summary>Returns true when JetBrains Rider is installed.</summary>
    public bool IsRiderInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        string riderPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "JetBrains", "Toolbox", "apps", "Rider");

        return Directory.Exists(riderPath);
    }

    /// <summary>Performs all system checks and returns a summary list.</summary>
    public List<(string Check, bool Passed)> RunAll(SetupConfiguration config)
    {
        var results = new List<(string, bool)>
        {
            ("Operating System: Windows", RuntimeInformation.IsOSPlatform(OSPlatform.Windows)),
            ("Working Directory Writable", IsDirectoryWritable(config.WorkingDirectory)),
            (".NET 10 Runtime", IsDotNetPresent())
        };

        return results;
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            string parent = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path) ?? Path.GetPathRoot(path) ?? path;

            string probe = Path.Combine(parent, Path.GetRandomFileName());
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDotNetPresent()
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
