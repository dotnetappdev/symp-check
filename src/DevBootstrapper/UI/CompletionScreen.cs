using DevBootstrapper.Models;

namespace DevBootstrapper.UI;

/// <summary>Renders the Step 11 completion screen to the console.</summary>
public static class CompletionScreen
{
    public static void Show(SetupConfiguration config, List<InstallTask> tasks, string logPath)
    {
        Console.Clear();

        // ── Header ────────────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╔══════════════════════════════════════════════════╗");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ║                                                  ║");
        Console.WriteLine("  ║             ✔  Setup Complete                    ║");
        Console.WriteLine("  ║                                                  ║");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╚══════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // ── Task summary ──────────────────────────────────────────────────────
        foreach (var task in tasks)
        {
            string icon = task.Status switch
            {
                InstallStatus.Completed => "  ✓",
                InstallStatus.Failed    => "  ✗",
                InstallStatus.Skipped   => "  —",
                _                       => "  ?"
            };

            ConsoleColor color = task.Status switch
            {
                InstallStatus.Completed => ConsoleColor.Green,
                InstallStatus.Failed    => ConsoleColor.Red,
                _                       => ConsoleColor.Gray
            };

            Console.ForegroundColor = color;
            Console.Write(icon + "  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(task.Name);
            Console.ResetColor();

            if (task.Status == InstallStatus.Failed && task.ErrorMessage is not null)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"        Error: {task.ErrorMessage}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();

        // ── Footer info ───────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Working Directory: ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(config.WorkingDirectory);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Log Location:      ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(logPath);
        Console.ResetColor();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Press any key to exit.");
        Console.ResetColor();
        Console.ReadKey(intercept: true);
    }
}
