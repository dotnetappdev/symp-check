using DevBootstrapper.Installers;
using DevBootstrapper.Services;
using DevBootstrapper.UI;
using DevBootstrapper.Wizard;

// Services
var packageProvider = new PackageProviderService();
var git = new GitService();
var sysCheck = new SystemCheckService();
using var log = new LogService();
var configFileService = new ConfigurationFileService();

log.Log("Developer Environment Bootstrapper started.");

// Load bootstrapper.ini if present (pre-populates wizard defaults)
string configPath = ConfigurationFileService.ResolveConfigPath();
var savedConfig = configFileService.Load(configPath);

if (savedConfig is not null)
    log.Log($"Loaded configuration from: {configPath}");

// --silent flag: skip the wizard and install directly from bootstrapper.ini
bool unattended = args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

DevBootstrapper.Models.SetupConfiguration config;
if (unattended && savedConfig is not null)
{
    log.Log("Unattended mode: using configuration from bootstrapper.ini.");
    config = savedConfig;
}
else
{
    // Wizard (Steps 1–11)
    var wizard = new SetupWizard(git, sysCheck);
    config = wizard.Run(savedConfig);

    // Persist completed configuration so the next run can use it as defaults
    try
    {
        configFileService.Save(config, configPath);
        log.Log($"Configuration saved to: {configPath}");
    }
    catch (Exception ex)
    {
        log.Log($"Warning: could not save configuration file – {ex.Message}");
    }
}

// Build task list
var tasks = TaskListBuilder.Build(config);

// Orchestrator
var orchestrator = new InstallOrchestrator(packageProvider, git, sysCheck, log);

// Installation progress dashboard
var dashboard = new ProgressDashboard(tasks, log.LogPath);
dashboard.Show(async logLine =>
{
    await orchestrator.RunAsync(config, tasks, task =>
    {
        logLine($"[{task.Status}] {task.Name}");
    });
});

// Completion screen
CompletionScreen.Show(config, tasks, log.LogPath);
