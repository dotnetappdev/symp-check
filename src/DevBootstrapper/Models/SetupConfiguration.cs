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

/// <summary>Holds all answers gathered during the wizard steps.</summary>
public sealed class SetupConfiguration
{
    public EnvironmentType Environment { get; set; } = EnvironmentType.SymphonyMessenger;
    public string WorkingDirectory { get; set; } = @"C:\Work";
    public bool CloneDefaultRepository { get; set; } = true;
    public List<string> AdditionalRepositories { get; set; } = [];
    public PackageProvider PackageProvider { get; set; } = PackageProvider.AutoDetect;
    public VisualStudioEdition VisualStudioEdition { get; set; } = VisualStudioEdition.Community;

    /// <summary>Visual Studio product key (never logged).</summary>
    public string? VisualStudioProductKey { get; set; }

    public bool InstallRider { get; set; } = true;
    public bool InstallDotNetFramework48 { get; set; } = true;
    public bool InstallGit { get; set; } = true;

    // Custom-mode tool selections
    public List<string> CustomTools { get; set; } = [];

    public static string DefaultRepository =>
        "https://github.com/<organisation>/SymphonyMessenger.git";
}
