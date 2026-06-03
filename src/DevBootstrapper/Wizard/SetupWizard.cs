using DevBootstrapper.Models;
using DevBootstrapper.Services;

namespace DevBootstrapper.Wizard;

/// <summary>Contains all interactive wizard steps run in sequence before installation.</summary>
public sealed class SetupWizard
{
    private readonly PackageProviderService _packageProvider;
    private readonly GitService _git;
    private readonly SystemCheckService _sysCheck;

    public SetupWizard(
        PackageProviderService packageProvider,
        GitService git,
        SystemCheckService sysCheck)
    {
        _packageProvider = packageProvider;
        _git = git;
        _sysCheck = sysCheck;
    }

    /// <summary>Runs all wizard steps and returns the completed configuration.</summary>
    public SetupConfiguration Run()
    {
        PrintBanner();

        var config = new SetupConfiguration();

        // Step 1 – Environment
        config.Environment = Step1_SelectEnvironment();

        // Step 2 – Working directory
        config.WorkingDirectory = Step2_WorkingDirectory();

        // Step 3 – Repository setup
        if (config.Environment == EnvironmentType.SymphonyMessenger)
        {
            (config.CloneDefaultRepository, config.AdditionalRepositories) =
                Step3_RepositorySetup();
        }

        // Step 4 – Package provider
        config.PackageProvider = Step4_PackageProvider();

        // Steps 5–8 – Tool selection (same prompts regardless of environment)
        config.VisualStudioEdition = Step5_VisualStudio();
        config.InstallRider = Step6_JetBrainsRider();
        config.InstallDotNetFramework48 = Step7_DotNetFramework();
        config.InstallGit = Step8_Git();

        // Step 9 – Review
        bool proceed = Step9_Review(config);
        if (!proceed)
        {
            Console.WriteLine("Installation cancelled. Exiting.");
            Environment.Exit(0);
        }

        return config;
    }

    // -------------------------------------------------------------------------
    // Step 1
    // -------------------------------------------------------------------------
    private static EnvironmentType Step1_SelectEnvironment()
    {
        PrintStepHeader(1, "Select Environment");
        WriteColored("Which environment would you like to set up?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", "Symphony Messenger");
        WriteChoice("2", "Custom");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": return EnvironmentType.SymphonyMessenger;
                case "2": return EnvironmentType.Custom;
                default:
                    WriteWarning("Please enter 1 or 2.");
                    break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step 2
    // -------------------------------------------------------------------------
    private static string Step2_WorkingDirectory()
    {
        const string defaultDir = @"C:\Work";

        PrintStepHeader(2, "Working Directory");
        WriteColored("Default: ", ConsoleColor.Gray);
        WriteColored(defaultDir, ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine();
        WriteColored("Use default?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("Y", "Yes – use default");
        WriteChoice("N", "No  – enter custom path");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice [Y/N]");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (input == "Y" || input == "")
                return defaultDir;

            if (input == "N")
            {
                Console.WriteLine();
                WritePrompt("Enter working directory");
                string? custom = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(custom))
                    return custom;

                WriteWarning("Directory cannot be empty.");
            }
            else
            {
                WriteWarning("Please enter Y or N.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step 3
    // -------------------------------------------------------------------------
    private static (bool cloneDefault, List<string> additional) Step3_RepositorySetup()
    {
        PrintStepHeader(3, "Repository Setup");
        WriteColored("Clone Symphony Messenger repositories?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("Y", "Yes");
        WriteChoice("N", "No");
        Console.WriteLine();

        bool cloneDefault = PromptYesNo("Clone repositories?", defaultYes: true);

        var additional = new List<string>();

        if (cloneDefault)
        {
            Console.WriteLine();
            WriteColored("  Default: ", ConsoleColor.Gray);
            WriteColored(SetupConfiguration.DefaultRepository, ConsoleColor.Cyan);
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine();
                bool addMore = PromptYesNo("Would you like to add another repository?", defaultYes: false);
                if (!addMore) break;

                WritePrompt("Repository URL");
                string? url = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(url))
                    additional.Add(url);
            }
        }

        return (cloneDefault, additional);
    }

    // -------------------------------------------------------------------------
    // Step 4
    // -------------------------------------------------------------------------
    private PackageProvider Step4_PackageProvider()
    {
        PrintStepHeader(4, "Package Provider");
        WriteColored("Select installation provider", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", "Auto Detect  (recommended)");
        WriteChoice("2", "Winget");
        WriteChoice("3", "Chocolatey");
        Console.WriteLine();

        PackageProvider choice;
        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": choice = PackageProvider.AutoDetect; goto done;
                case "2": choice = PackageProvider.Winget; goto done;
                case "3": choice = PackageProvider.Chocolatey; goto done;
                default:
                    WriteWarning("Please enter 1, 2, or 3.");
                    break;
            }
        }

        done:
        // Warn if Chocolatey is selected but not installed
        if (choice == PackageProvider.Chocolatey && !_packageProvider.IsChocolateyAvailable())
        {
            Console.WriteLine();
            WriteWarning("Chocolatey was not found.");
            Console.WriteLine();
            bool install = PromptYesNo("Install Chocolatey?", defaultYes: true);
            if (!install)
            {
                WriteWarning("Falling back to Winget.");
                choice = PackageProvider.Winget;
            }
        }

        return choice;
    }

    // -------------------------------------------------------------------------
    // Step 5
    // -------------------------------------------------------------------------
    private static VisualStudioEdition Step5_VisualStudio()
    {
        PrintStepHeader(5, "Visual Studio");
        WriteColored("Install Visual Studio?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", "Community");
        WriteChoice("2", "Professional");
        WriteChoice("3", "Skip");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": return VisualStudioEdition.Community;
                case "2":
                    Console.WriteLine();
                    WritePrompt("Enter Product Key (optional, press Enter to skip)");
                    // Read but do not store in any variable that could be logged
                    ReadProductKeySecurely();
                    return VisualStudioEdition.Professional;
                case "3": return VisualStudioEdition.Skip;
                default:
                    WriteWarning("Please enter 1, 2, or 3.");
                    break;
            }
        }
    }

    /// <summary>Reads a product key character by character; the value is discarded after use.</summary>
    private static string ReadProductKeySecurely()
    {
        var key = new System.Text.StringBuilder();
        ConsoleKeyInfo ki;
        while ((ki = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (ki.Key == ConsoleKey.Backspace && key.Length > 0)
            {
                key.Remove(key.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (ki.Key != ConsoleKey.Backspace)
            {
                key.Append(ki.KeyChar);
                Console.Write('*');
            }
        }

        Console.WriteLine();
        string result = key.ToString();
        key.Clear(); // clear from memory ASAP
        return result;
    }

    // -------------------------------------------------------------------------
    // Step 6
    // -------------------------------------------------------------------------
    private static bool Step6_JetBrainsRider()
    {
        PrintStepHeader(6, "JetBrains Rider");
        Console.WriteLine();
        return PromptYesNo("Install JetBrains Rider?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 7
    // -------------------------------------------------------------------------
    private bool Step7_DotNetFramework()
    {
        PrintStepHeader(7, ".NET Framework 4.8");

        if (_sysCheck.IsDotNetFramework48Installed())
        {
            WriteColored("  ✓  .NET Framework 4.8 is already installed.", ConsoleColor.Green);
            Console.WriteLine();
            return false; // no action needed
        }

        return PromptYesNo("Install .NET Framework 4.8?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 8
    // -------------------------------------------------------------------------
    private bool Step8_Git()
    {
        PrintStepHeader(8, "Git");

        if (_git.IsGitInstalled())
        {
            WriteColored("  ✓  Git is already installed.", ConsoleColor.Green);
            Console.WriteLine();
            return false;
        }

        WriteWarning("Git was not detected.");
        Console.WriteLine();
        return PromptYesNo("Install Git?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 9
    // -------------------------------------------------------------------------
    private static bool Step9_Review(SetupConfiguration config)
    {
        PrintStepHeader(9, "Review Configuration");

        WriteColored($"  Environment:       ", ConsoleColor.Gray);
        WriteColored(config.Environment.ToString(), ConsoleColor.Cyan);
        Console.WriteLine();
        WriteColored($"  Working Directory: ", ConsoleColor.Gray);
        WriteColored(config.WorkingDirectory, ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine();

        WriteColored("  Repositories:", ConsoleColor.White);
        Console.WriteLine();
        if (config.CloneDefaultRepository)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine(SetupConfiguration.DefaultRepository);
        }
        foreach (var repo in config.AdditionalRepositories)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine(repo);
        }

        Console.WriteLine();
        WriteColored("  Tools:", ConsoleColor.White);
        Console.WriteLine();
        if (config.VisualStudioEdition != VisualStudioEdition.Skip)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine($"Visual Studio {config.VisualStudioEdition}");
        }
        if (config.InstallRider)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine("JetBrains Rider");
        }
        if (config.InstallDotNetFramework48)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine(".NET Framework 4.8");
        }
        if (config.InstallGit)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            Console.WriteLine("Git");
        }

        Console.WriteLine();
        WriteColored("Proceed with installation?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("Y", "Install");
        WriteChoice("N", "Cancel");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice [Y/N]");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (input == "Y" || input == "") return true;
            if (input == "N") return false;
            WriteWarning("Please enter Y or N.");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static bool PromptYesNo(string prompt, bool defaultYes)
    {
        string hint = defaultYes ? "[Y/n]" : "[y/N]";
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {prompt} ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(hint);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ResetColor();

            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(input))
                return defaultYes;

            if (input == "Y") return true;
            if (input == "N") return false;

            WriteWarning("Please enter Y or N.");
        }
    }

    private static void PrintBanner()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("║       Developer Environment Bootstrapper         ║");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>Prints a vivid numbered step header with a separator line.</summary>
    private static void PrintStepHeader(int step, string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('─', 52));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  Step {step}");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(" ── ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(title);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('─', 52));
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>Writes text in the specified <paramref name="color"/> then resets.</summary>
    private static void WriteColored(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    /// <summary>Writes a numbered/keyed menu choice with bright formatting.</summary>
    private static void WriteChoice(string key, string description)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [{key}]");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(description);
        Console.ResetColor();
    }

    /// <summary>Writes a bright prompt marker and label.</summary>
    private static void WritePrompt(string label)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ❯ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{label}: ");
        Console.ResetColor();
    }

    /// <summary>Writes a yellow warning line.</summary>
    private static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠  {message}");
        Console.ResetColor();
    }
}
