using DevBootstrapper.Models;

namespace DevBootstrapper.Wizard;

/// <summary>Builds the ordered list of <see cref="InstallTask"/> objects from a <see cref="SetupConfiguration"/>.</summary>
public static class TaskListBuilder
{
    public static List<InstallTask> Build(SetupConfiguration config)
    {
        var tasks = new List<InstallTask>
        {
            new() { Name = "Working Directory" }
        };

        if (config.PackageProvider == PackageProvider.Chocolatey)
            tasks.Add(new() { Name = "Install Chocolatey" });

        if (config.InstallGit)
            tasks.Add(new() { Name = "Install Git" });

        if (config.CloneDefaultRepository)
        {
            string repoName = ExtractRepoName(SetupConfiguration.DefaultRepository);
            tasks.Add(new() { Name = $"Clone {repoName}" });
        }

        foreach (var repo in config.AdditionalRepositories)
            tasks.Add(new() { Name = $"Clone {ExtractRepoName(repo)}" });

        if (config.VisualStudioEdition != VisualStudioEdition.Skip)
            tasks.Add(new() { Name = $"Install Visual Studio {config.VisualStudioEdition}" });

        if (config.InstallRider)
            tasks.Add(new() { Name = "Install JetBrains Rider" });

        if (config.InstallDotNetFramework48)
            tasks.Add(new() { Name = "Install .NET Framework 4.8" });

        return tasks;
    }

    private static string ExtractRepoName(string url)
    {
        string last = url.TrimEnd('/').Split('/').Last();
        return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? last[..^4]
            : last;
    }
}
