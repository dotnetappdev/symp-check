namespace DevBootstrapper.Models;

/// <summary>A single installation task with its current status.</summary>
public sealed class InstallTask
{
    public string Name { get; init; } = string.Empty;
    public InstallStatus Status { get; set; } = InstallStatus.Pending;
    public string? ErrorMessage { get; set; }
}

/// <summary>Status of an individual installation task.</summary>
public enum InstallStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}
