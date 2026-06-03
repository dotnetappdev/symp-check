using DevBootstrapper.Models;
using DevBootstrapper.Services;

namespace DevBootstrapper.Installers;

/// <summary>Orchestrates the installation of all selected packages.</summary>
public sealed class InstallOrchestrator
{
    private readonly PackageProviderService _provider;
    private readonly GitService _git;
    private readonly SystemCheckService _sysCheck;
    private readonly LogService _log;

    public InstallOrchestrator(
        PackageProviderService provider,
        GitService git,
        SystemCheckService sysCheck,
        LogService log)
    {
        _provider = provider;
        _git = git;
        _sysCheck = sysCheck;
        _log = log;
    }

    /// <summary>
    /// Runs the full installation based on the provided configuration.
    /// Progress updates are reported through <paramref name="onTaskUpdate"/>.
    /// </summary>
    public async Task RunAsync(
        SetupConfiguration config,
        List<InstallTask> tasks,
        Action<InstallTask> onTaskUpdate)
    {
        PackageProvider resolvedProvider = _provider.Resolve(config.PackageProvider);

        // Working directory
        var dirTask = GetTask(tasks, "Working Directory");
        try
        {
            dirTask.Status = InstallStatus.Running;
            onTaskUpdate(dirTask);
            Directory.CreateDirectory(config.WorkingDirectory);
            dirTask.Status = InstallStatus.Completed;
            _log.Log($"Working directory ready: {config.WorkingDirectory}");
        }
        catch (Exception ex)
        {
            dirTask.Status = InstallStatus.Failed;
            dirTask.ErrorMessage = ex.Message;
            _log.Log($"Failed to create directory: {ex.Message}");
        }

        onTaskUpdate(dirTask);

        // Chocolatey install (if needed)
        if (resolvedProvider == PackageProvider.Chocolatey && !_provider.IsChocolateyAvailable())
        {
            var chocoTask = GetTask(tasks, "Install Chocolatey");
            chocoTask.Status = InstallStatus.Running;
            onTaskUpdate(chocoTask);

            bool ok = await _provider.InstallChocolateyAsync(_log.Log);
            chocoTask.Status = ok ? InstallStatus.Completed : InstallStatus.Failed;
            onTaskUpdate(chocoTask);
        }

        // Git
        if (config.InstallGit)
        {
            await RunPackageTask(tasks, "Install Git", async () =>
            {
                if (_git.IsGitInstalled())
                {
                    _log.Log("Git is already installed, skipping.");
                    return true;
                }

                return await _provider.InstallPackageAsync(
                    resolvedProvider, "Git.Git", null, _log.Log, config.SilentInstall);
            }, onTaskUpdate);
        }

        // Clone repositories
        if (config.CloneDefaultRepository || config.AdditionalRepositories.Count > 0)
        {
            var repos = new List<string>();
            if (config.CloneDefaultRepository)
                repos.Add(SetupConfiguration.DefaultRepository);
            repos.AddRange(config.AdditionalRepositories);

            foreach (var repo in repos)
            {
                string repoName = ExtractRepoName(repo);
                string cloneTarget = Path.Combine(config.WorkingDirectory, repoName);

                await RunPackageTask(tasks, $"Clone {repoName}", async () =>
                {
                    if (Directory.Exists(cloneTarget))
                    {
                        _log.Log($"Repository directory {cloneTarget} already exists, skipping clone.");
                        return true;
                    }

                    return await _git.CloneAsync(repo, config.WorkingDirectory, _log.Log);
                }, onTaskUpdate);
            }
        }

        // Visual Studio
        if (config.VisualStudioEdition != VisualStudioEdition.Skip)
        {
            string vsYear = config.VisualStudioVersion switch
            {
                VisualStudioVersion.VS2019 => "2019",
                VisualStudioVersion.VS2022 => "2022",
                _ => "2022"
            };

            string vsTaskName = $"Install Visual Studio {config.VisualStudioEdition} {vsYear}";
            await RunPackageTask(tasks, vsTaskName, async () =>
            {
                if (_sysCheck.IsVisualStudioInstalled(config.VisualStudioVersion))
                {
                    _log.Log($"Visual Studio {vsYear} is already installed, skipping.");
                    return true;
                }

                string packageId = (config.VisualStudioEdition, config.VisualStudioVersion) switch
                {
                    (VisualStudioEdition.Community,    VisualStudioVersion.VS2019) => "Microsoft.VisualStudio.2019.Community",
                    (VisualStudioEdition.Professional, VisualStudioVersion.VS2019) => "Microsoft.VisualStudio.2019.Professional",
                    (VisualStudioEdition.Community,    VisualStudioVersion.VS2022) => "Microsoft.VisualStudio.2022.Community",
                    (VisualStudioEdition.Professional, VisualStudioVersion.VS2022) => "Microsoft.VisualStudio.2022.Professional",
                    _ => "Microsoft.VisualStudio.2022.Community"
                };

                // Recommended workloads; /norestart added when silent install is requested
                string vsInstallMode = config.SilentInstall ? "--quiet --norestart" : "--quiet";
                string workloadArgs = resolvedProvider == PackageProvider.Winget
                    ? $"--override \"{vsInstallMode} --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.Workload.NetWeb --includeRecommended\""
                    : null!;

                bool installed = await _provider.InstallPackageAsync(
                    resolvedProvider, packageId, workloadArgs, _log.Log, config.SilentInstall);

                // Product key is handled via VS activation UI; never logged
                return installed;
            }, onTaskUpdate);
        }

        // JetBrains Rider
        if (config.InstallRider)
        {
            await RunPackageTask(tasks, "Install JetBrains Rider", async () =>
            {
                if (_sysCheck.IsRiderInstalled())
                {
                    _log.Log("JetBrains Rider is already installed, skipping.");
                    return true;
                }

                // Append --version when a specific version is requested
                string? versionArg = null;
                if (!string.IsNullOrWhiteSpace(config.RiderVersion) &&
                    !config.RiderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase))
                {
                    versionArg = resolvedProvider == PackageProvider.Winget
                        ? $"--version {config.RiderVersion}"
                        : $"--version {config.RiderVersion}";
                }

                return await _provider.InstallPackageAsync(
                    resolvedProvider, "JetBrains.Rider", versionArg, _log.Log, config.SilentInstall);
            }, onTaskUpdate);
        }

        // .NET Framework 4.8
        if (config.InstallDotNetFramework48)
        {
            await RunPackageTask(tasks, "Install .NET Framework 4.8", async () =>
            {
                if (_sysCheck.IsDotNetFramework48Installed())
                {
                    _log.Log(".NET Framework 4.8 is already installed, skipping.");
                    return true;
                }

                bool ok = await _provider.InstallPackageAsync(
                    resolvedProvider, "Microsoft.DotNet.Framework.DeveloperPack_4", null, _log.Log, config.SilentInstall);

                if (ok && !_sysCheck.IsDotNetFramework48Installed())
                {
                    _log.Log("WARNING: .NET Framework 4.8 installation could not be verified.");
                }

                return ok;
            }, onTaskUpdate);
        }

        // Additional winget packages
        foreach (string pkgId in config.AdditionalWingetPackages)
        {
            string capturedId = pkgId;
            await RunPackageTask(tasks, $"Install {capturedId}", async () =>
            {
                _log.Log($"Installing additional package: {capturedId}");
                return await _provider.InstallPackageAsync(
                    PackageProvider.Winget, capturedId, null, _log.Log, config.SilentInstall);
            }, onTaskUpdate);
        }
    }

    private static async Task RunPackageTask(
        List<InstallTask> tasks,
        string taskName,
        Func<Task<bool>> action,
        Action<InstallTask> onTaskUpdate)
    {
        var task = GetTask(tasks, taskName);
        task.Status = InstallStatus.Running;
        onTaskUpdate(task);

        try
        {
            bool ok = await action();
            task.Status = ok ? InstallStatus.Completed : InstallStatus.Failed;
        }
        catch (Exception ex)
        {
            task.Status = InstallStatus.Failed;
            task.ErrorMessage = ex.Message;
        }

        onTaskUpdate(task);
    }

    private static InstallTask GetTask(List<InstallTask> tasks, string name)
    {
        var t = tasks.FirstOrDefault(t => t.Name == name);
        if (t is null)
        {
            t = new InstallTask { Name = name };
            tasks.Add(t);
        }

        return t;
    }

    private static string ExtractRepoName(string url)
    {
        string last = url.TrimEnd('/').Split('/').Last();
        return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? last[..^4]
            : last;
    }
}
