using DevBootstrapper.Installers;
using DevBootstrapper.Services;
using DevBootstrapper.UI;
using DevBootstrapper.Wizard;

// Services
var packageProvider = new PackageProviderService();
var git = new GitService();
var sysCheck = new SystemCheckService();
using var log = new LogService();

log.Log("Developer Environment Bootstrapper started.");

// Wizard (Steps 1–9)
var wizard = new SetupWizard(packageProvider, git, sysCheck);
var config = wizard.Run();

// Build task list (Step 10 input)
var tasks = TaskListBuilder.Build(config);

// Orchestrator
var orchestrator = new InstallOrchestrator(packageProvider, git, sysCheck, log);

// Step 10 – Installation progress dashboard
// The logLine callback is wired to both the log service and the task-update handler
// so that every task status change triggers a live UI refresh.
var dashboard = new ProgressDashboard(tasks, log.LogPath);
dashboard.Show(async logLine =>
{
    await orchestrator.RunAsync(config, tasks, task =>
    {
        // Forward task-status changes as log entries to drive dashboard refresh
        logLine($"[{task.Status}] {task.Name}");
    });
});

// Step 11 – Completion screen
CompletionScreen.Show(config, tasks, log.LogPath);
