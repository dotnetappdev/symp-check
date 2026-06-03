using DevBootstrapper.Models;

namespace DevBootstrapper.UI;

/// <summary>Renders the Step 11 completion screen to the console.</summary>
public static class CompletionScreen
{
    public static void Show(SetupConfiguration config, List<InstallTask> tasks, string logPath)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║                  Setup Complete                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        foreach (var task in tasks)
        {
            string icon = task.Status switch
            {
                InstallStatus.Completed => "✓",
                InstallStatus.Failed => "✗",
                InstallStatus.Skipped => "—",
                _ => "?"
            };

            ConsoleColor color = task.Status == InstallStatus.Completed
                ? ConsoleColor.Green
                : task.Status == InstallStatus.Failed
                    ? ConsoleColor.Red
                    : ConsoleColor.Gray;

            Console.ForegroundColor = color;
            Console.WriteLine($"  {icon} {task.Name}");
            Console.ResetColor();

            if (task.Status == InstallStatus.Failed && task.ErrorMessage is not null)
                Console.WriteLine($"      Error: {task.ErrorMessage}");
        }

        Console.WriteLine();
        Console.WriteLine($"  Working Directory: {config.WorkingDirectory}");
        Console.WriteLine();
        Console.WriteLine($"  Log Location: {logPath}");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey(intercept: true);
    }
}
