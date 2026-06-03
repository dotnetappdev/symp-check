using System.Collections.ObjectModel;
using DevBootstrapper.Models;
using Terminal.Gui;

namespace DevBootstrapper.UI;

/// <summary>
/// Displays a Terminal.Gui dashboard showing overall progress, task list,
/// and a live log panel while installation is running.
/// Uses vivid 16-colour schemes for maximum visual clarity.
/// </summary>
public sealed class ProgressDashboard
{
    private readonly List<InstallTask> _tasks;
    private readonly string _logPath;

    private ProgressBar? _overallProgress;
    private Label? _currentTaskLabel;
    private ListView? _taskListView;
    private ListView? _logView;

    private readonly ObservableCollection<string> _taskItems = [];
    private readonly ObservableCollection<string> _logItems = [];

    private readonly object _lock = new();

    // ── Colour schemes ────────────────────────────────────────────────────────
    // Mirrors the bold palette used in Terminal.Gui's own demo applications.
    private static readonly ColorScheme SchemeBase      = MakeScheme(ColorName16.White,         ColorName16.Black);
    private static readonly ColorScheme SchemeTitle     = MakeScheme(ColorName16.BrightYellow,  ColorName16.Black);
    private static readonly ColorScheme SchemeProgress  = MakeScheme(ColorName16.BrightGreen,   ColorName16.Black);
    private static readonly ColorScheme SchemeTask      = MakeScheme(ColorName16.BrightCyan,    ColorName16.Black);
    private static readonly ColorScheme SchemeLog       = MakeScheme(ColorName16.BrightMagenta, ColorName16.Black);
    private static readonly ColorScheme SchemeLogText   = MakeScheme(ColorName16.BrightYellow,  ColorName16.Black);
    private static readonly ColorScheme SchemeTaskText  = MakeScheme(ColorName16.White,         ColorName16.Black);
    private static readonly ColorScheme SchemeCurrent   = MakeScheme(ColorName16.BrightCyan,    ColorName16.Black);

    public ProgressDashboard(List<InstallTask> tasks, string logPath)
    {
        _tasks = tasks;
        _logPath = logPath;
    }

    /// <summary>
    /// Shows the dashboard and runs <paramref name="installWork"/> on a background thread.
    /// Blocks until installation is complete.
    /// </summary>
    public void Show(Func<Action<string>, Task> installWork)
    {
        Application.Force16Colors = true;   // enable the vivid 16-colour palette
        Application.Init();

        BuildUi();

        // Start background work immediately – Task.Run before Application.Run
        _ = Task.Run(async () =>
        {
            await installWork(line =>
            {
                lock (_lock)
                {
                    _logItems.Add($"[{DateTime.Now:HH:mm}] {line}");
                    // Keep at most 200 lines
                    if (_logItems.Count > 200)
                        _logItems.RemoveAt(0);
                }

                Application.Invoke(RefreshUi);
            });

            Application.Invoke(() =>
            {
                RefreshUi();
                Application.RequestStop();
            });
        });

        Application.Run();
        Application.Shutdown();
    }

    private void BuildUi()
    {
        var top = Application.Top ?? throw new InvalidOperationException("Application.Top is null; call Application.Init() first.");
        top.ColorScheme = SchemeBase;

        // ── Title banner ──────────────────────────────────────────────────────
        var titleLabel = new Label
        {
            Text = "══  Developer Environment Bootstrapper  ══",
            X = Pos.Center(),
            Y = 0,
            ColorScheme = SchemeTitle
        };

        // ── Overall progress frame ────────────────────────────────────────────
        var progressFrame = new FrameView
        {
            Title = "Overall Progress",
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 4,
            ColorScheme = SchemeProgress
        };

        _overallProgress = new ProgressBar
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()! - Dim.Absolute(2)!,
            Fraction = 0f,
            ColorScheme = SchemeProgress
        };
        progressFrame.Add(_overallProgress);

        // ── Current-task label ────────────────────────────────────────────────
        _currentTaskLabel = new Label
        {
            Text = "Initialising...",
            X = 2,
            Y = 7,
            ColorScheme = SchemeCurrent
        };

        // ── Task list panel ───────────────────────────────────────────────────
        var taskFrame = new FrameView
        {
            Title = "Tasks",
            X = 0,
            Y = 9,
            Width = Dim.Percent(50),
            Height = Dim.Fill()! - Dim.Absolute(10)!,
            ColorScheme = SchemeTask
        };

        _taskListView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
            ColorScheme = SchemeTaskText
        };
        _taskListView.SetSource(_taskItems);
        taskFrame.Add(_taskListView);

        // ── Live log panel ────────────────────────────────────────────────────
        var logFrame = new FrameView
        {
            Title = "Live Log",
            X = Pos.Percent(50),
            Y = 9,
            Width = Dim.Fill(),
            Height = Dim.Fill()! - Dim.Absolute(10)!,
            ColorScheme = SchemeLog
        };

        _logView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
            ColorScheme = SchemeLogText
        };
        _logView.SetSource(_logItems);
        logFrame.Add(_logView);

        top.Add(titleLabel, progressFrame, _currentTaskLabel, taskFrame, logFrame);
    }

    private void RefreshUi()
    {
        int done = _tasks.Count(t =>
            t.Status is InstallStatus.Completed or InstallStatus.Failed or InstallStatus.Skipped);

        float fraction = _tasks.Count > 0 ? (float)done / _tasks.Count : 0f;

        if (_overallProgress is not null)
            _overallProgress.Fraction = fraction;

        var running = _tasks.FirstOrDefault(t => t.Status == InstallStatus.Running);
        if (_currentTaskLabel is not null)
        {
            _currentTaskLabel.Text = running is not null
                ? $"Current Task: {running.Name}"
                : done == _tasks.Count ? "All tasks complete." : "Waiting...";
        }

        // Update task list items in place so the ObservableCollection notifies
        var fresh = BuildTaskStrings();
        lock (_lock)
        {
            for (int i = 0; i < fresh.Count; i++)
            {
                if (i < _taskItems.Count)
                    _taskItems[i] = fresh[i];
                else
                    _taskItems.Add(fresh[i]);
            }

            while (_taskItems.Count > fresh.Count)
                _taskItems.RemoveAt(_taskItems.Count - 1);

            if (_logView is not null && _logItems.Count > 0)
                _logView.SelectedItem = _logItems.Count - 1;
        }
    }

    private List<string> BuildTaskStrings()
    {
        return _tasks.Select(t => $"{StatusIcon(t.Status)} {t.Name}").ToList();
    }

    private static string StatusIcon(InstallStatus status) => status switch
    {
        InstallStatus.Completed => "✓",
        InstallStatus.Failed    => "✗",
        InstallStatus.Running   => "⟳",
        InstallStatus.Skipped   => "—",
        _                       => "□"
    };

    /// <summary>
    /// Creates a vivid <see cref="ColorScheme"/> with the given foreground/background pair.
    /// The hot-key and focus attributes are set to complementary bright colours.
    /// </summary>
    private static ColorScheme MakeScheme(ColorName16 fg, ColorName16 bg) => new()
    {
        Normal    = new Terminal.Gui.Attribute(fg, bg),
        Focus     = new Terminal.Gui.Attribute(ColorName16.White,        ColorName16.BrightBlue),
        HotNormal = new Terminal.Gui.Attribute(ColorName16.BrightYellow, bg),
        HotFocus  = new Terminal.Gui.Attribute(ColorName16.BrightYellow, ColorName16.BrightBlue),
        Disabled  = new Terminal.Gui.Attribute(ColorName16.Gray,         bg)
    };
}
