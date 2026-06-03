namespace DevBootstrapper.Models;

/// <summary>Describes which environment preset is being configured.</summary>
public enum EnvironmentType
{
    SymphonyMessenger,
    Custom
}

/// <summary>Describes the package provider to use for installations.</summary>
public enum PackageProvider
{
    AutoDetect,
    Winget,
    Chocolatey
}

/// <summary>Describes the Visual Studio edition to install.</summary>
public enum VisualStudioEdition
{
    Community,
    Professional,
    Skip
}

/// <summary>Describes the Visual Studio release year to install.</summary>
public enum VisualStudioVersion
{
    VS2019,
    VS2022
}

/// <summary>Convenience extensions for <see cref="VisualStudioVersion"/>.</summary>
public static class VisualStudioVersionExtensions
{
    /// <summary>Returns the four-digit release year string (e.g. "2022").</summary>
    public static string ToYear(this VisualStudioVersion version) => version switch
    {
        VisualStudioVersion.VS2019 => "2019",
        VisualStudioVersion.VS2022 => "2022",
        _ => "2022"
    };
}

/// <summary>Holds all answers gathered during the wizard steps.</summary>
public sealed class SetupConfiguration
{
    public EnvironmentType Environment { get; set; } = EnvironmentType.SymphonyMessenger;
    public string WorkingDirectory { get; set; } = @"C:\Work";
    public bool CloneDefaultRepository { get; set; } = true;
    public List<string> AdditionalRepositories { get; set; } = [];
    public PackageProvider PackageProvider { get; set; } = PackageProvider.AutoDetect;
    public VisualStudioEdition VisualStudioEdition { get; set; } = VisualStudioEdition.Community;

    /// <summary>Visual Studio release year (e.g. 2019, 2022).</summary>
    public VisualStudioVersion VisualStudioVersion { get; set; } = VisualStudioVersion.VS2022;

    /// <summary>Visual Studio product key (never logged).</summary>
    public string? VisualStudioProductKey { get; set; }

    public bool InstallRider { get; set; } = true;

    /// <summary>Rider version to install. "latest" means the newest available; otherwise a specific version string, e.g. "2024.1".</summary>
    public string RiderVersion { get; set; } = "latest";

    public bool InstallDotNetFramework48 { get; set; } = true;
    public bool InstallGit { get; set; } = true;

    /// <summary>When true, package installers run in non-interactive / fully-silent mode.</summary>
    public bool SilentInstall { get; set; } = false;

    /// <summary>Extra winget package IDs to install after the standard tools.</summary>
    public List<string> AdditionalWingetPackages { get; set; } = [];

    // Custom-mode tool selections
    public List<string> CustomTools { get; set; } = [];

    public static string DefaultRepository =>
        "https://github.com/<organisation>/SymphonyMessenger.git";
}
