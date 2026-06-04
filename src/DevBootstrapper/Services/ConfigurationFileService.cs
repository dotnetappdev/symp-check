using DevBootstrapper.Models;

namespace DevBootstrapper.Services;

/// <summary>
/// Reads and writes the <c>bootstrapper.ini</c> configuration file that pre-populates
/// wizard defaults and enables unattended installation mode.
/// </summary>
public sealed class ConfigurationFileService
{
    /// <summary>
    /// Resolves the path to <c>bootstrapper.ini</c>.
    /// Checks the executable directory first; falls back to the current directory.
    /// </summary>
    public static string ResolveConfigPath()
    {
        string exeDir = AppContext.BaseDirectory;
        string exePath = Path.Combine(exeDir, "bootstrapper.ini");
        if (File.Exists(exePath))
            return exePath;

        return Path.Combine(Directory.GetCurrentDirectory(), "bootstrapper.ini");
    }

    /// <summary>
    /// Loads a <see cref="SetupConfiguration"/> from <paramref name="path"/>.
    /// Returns <c>null</c> when the file does not exist or cannot be parsed.
    /// </summary>
    public SetupConfiguration? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        var ini = ParseIni(path);
        var config = new SetupConfiguration();

        // [General]
        if (ini.TryGet("General", "WorkingDirectory", out string? wd) && !string.IsNullOrWhiteSpace(wd))
            config.WorkingDirectory = wd;

        if (ini.TryGet("General", "PackageProvider", out string? pp) &&
            Enum.TryParse<PackageProvider>(pp, ignoreCase: true, out var parsedPp) &&
            parsedPp == PackageProvider.Winget)
            config.PackageProvider = parsedPp;

        if (ini.TryGet("General", "SilentInstall", out string? si) &&
            bool.TryParse(si, out bool silentBool))
            config.SilentInstall = silentBool;

        // [VisualStudio]
        if (ini.TryGet("VisualStudio", "Edition", out string? ed) &&
            Enum.TryParse<VisualStudioEdition>(ed, ignoreCase: true, out var parsedEd))
            config.VisualStudioEdition = parsedEd;

        if (ini.TryGet("VisualStudio", "Version", out string? vsv) &&
            Enum.TryParse<VisualStudioVersion>(vsv, ignoreCase: true, out var parsedVsv))
            config.VisualStudioVersion = parsedVsv;

        // [Rider]
        if (ini.TryGet("Rider", "Install", out string? ri) &&
            bool.TryParse(ri, out bool riderBool))
            config.InstallRider = riderBool;

        if (ini.TryGet("Rider", "Version", out string? rv) && !string.IsNullOrWhiteSpace(rv))
            config.RiderVersion = rv.Trim();

        // [Git]
        if (ini.TryGet("Git", "Install", out string? gi) &&
            bool.TryParse(gi, out bool gitBool))
            config.InstallGit = gitBool;

        // [DotNetFramework48]
        if (ini.TryGet("DotNetFramework48", "Install", out string? df) &&
            bool.TryParse(df, out bool dfBool))
            config.InstallDotNetFramework48 = dfBool;

        // [AdditionalPackages]  –  Package1=id, Package2=id, …
        var packages = new List<string>();
        int n = 1;
        while (ini.TryGet("AdditionalPackages", $"Package{n}", out string? pkg))
        {
            if (!string.IsNullOrWhiteSpace(pkg))
                packages.Add(pkg.Trim());
            n++;
        }
        config.AdditionalWingetPackages = packages;

        // [WingetPackageIds]  –  override built-in package IDs with INI values
        foreach (var key in new[]
        {
            "Git", "DotNetFramework48", "Rider",
            "VisualStudio2022Community", "VisualStudio2022Professional",
            "VisualStudio2019Community", "VisualStudio2019Professional"
        })
        {
            if (ini.TryGet("WingetPackageIds", key, out string? pkgId) && !string.IsNullOrWhiteSpace(pkgId))
                config.WingetPackageIds[key] = pkgId.Trim();
        }

        return config;
    }

    /// <summary>Saves the current <paramref name="config"/> to <paramref name="path"/>.</summary>
    public void Save(SetupConfiguration config, string path)
    {
        var lines = new List<string>
        {
            "; DevBootstrapper Configuration File",
            "; Edit this file to pre-populate wizard defaults or enable unattended installation.",
            "; Unattended mode: run with --silent to skip the wizard entirely.",
            "",
            "[General]",
            $"WorkingDirectory={config.WorkingDirectory}",
            $"PackageProvider={config.PackageProvider}",
            $"SilentInstall={config.SilentInstall.ToString().ToLowerInvariant()}",
            "",
            "[VisualStudio]",
            $"Edition={config.VisualStudioEdition}",
            $"Version={config.VisualStudioVersion}",
            "",
            "[Rider]",
            $"Install={config.InstallRider.ToString().ToLowerInvariant()}",
            $"Version={config.RiderVersion}",
            "",
            "[Git]",
            $"Install={config.InstallGit.ToString().ToLowerInvariant()}",
            "",
            "[DotNetFramework48]",
            $"Install={config.InstallDotNetFramework48.ToString().ToLowerInvariant()}",
            "",
            "[AdditionalPackages]",
            "; Add extra winget package IDs to install.",
            "; Package1=Microsoft.PowerShell",
            "; Package2=Microsoft.WindowsTerminal"
        };

        for (int i = 0; i < config.AdditionalWingetPackages.Count; i++)
            lines.Add($"Package{i + 1}={config.AdditionalWingetPackages[i]}");

        lines.AddRange(new[]
        {
            "",
            "[WingetPackageIds]",
            "; Override the winget package IDs used for each known tool.",
            "; Remove or comment out a line to revert to the built-in default."
        });

        foreach (var kvp in config.WingetPackageIds)
            lines.Add($"{kvp.Key}={kvp.Value}");

        File.WriteAllLines(path, lines);
    }

    // -------------------------------------------------------------------------
    // INI parsing
    // -------------------------------------------------------------------------

    private sealed class IniData
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new(StringComparer.OrdinalIgnoreCase);

        public void Set(string section, string key, string value)
        {
            if (!_sections.TryGetValue(section, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[section] = dict;
            }

            dict[key] = value;
        }

        public bool TryGet(string section, string key, out string? value)
        {
            if (_sections.TryGetValue(section, out var dict) &&
                dict.TryGetValue(key, out string? v))
            {
                value = v;
                return true;
            }

            value = null;
            return false;
        }
    }

    private static IniData ParseIni(string path)
    {
        var data = new IniData();
        string currentSection = string.Empty;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();

            // Skip blank lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            // Section header  (line is at least "[]" → 2 chars, so [1..^1] is always safe)
            if (line.StartsWith('[') && line.EndsWith(']') && line.Length >= 2)
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            // Key=Value
            int eqIdx = line.IndexOf('=');
            if (eqIdx > 0 && !string.IsNullOrWhiteSpace(currentSection))
            {
                string key = line[..eqIdx].Trim();
                string val = line[(eqIdx + 1)..].Trim();

                // Strip inline comments
                int commentIdx = val.IndexOf(';');
                if (commentIdx >= 0)
                    val = val[..commentIdx].Trim();

                data.Set(currentSection, key, val);
            }
        }

        return data;
    }
}
