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
        Console.WriteLine();
        Console.WriteLine("=== Step 1 – Select Environment ===");
        Console.WriteLine();
        Console.WriteLine("Which environment would you like to set up?");
        Console.WriteLine();
        Console.WriteLine("  1. Symphony Messenger");
        Console.WriteLine("  2. Custom");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Choice: ");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": return EnvironmentType.SymphonyMessenger;
                case "2": return EnvironmentType.Custom;
                default:
                    Console.WriteLine("Please enter 1 or 2.");
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

        Console.WriteLine();
        Console.WriteLine("=== Step 2 – Working Directory ===");
        Console.WriteLine();
        Console.WriteLine($"Default: {defaultDir}");
        Console.WriteLine();
        Console.WriteLine("Use default?");
        Console.WriteLine();
        Console.WriteLine("  [Y] Yes");
        Console.WriteLine("  [N] Choose Custom");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Choice [Y/N]: ");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (input == "Y" || input == "")
                return defaultDir;

            if (input == "N")
            {
                Console.WriteLine();
                Console.Write("Enter working directory: ");
                string? custom = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(custom))
                    return custom;

                Console.WriteLine("Directory cannot be empty.");
            }
            else
            {
                Console.WriteLine("Please enter Y or N.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step 3
    // -------------------------------------------------------------------------
    private static (bool cloneDefault, List<string> additional) Step3_RepositorySetup()
    {
        Console.WriteLine();
        Console.WriteLine("=== Step 3 – Repository Setup ===");
        Console.WriteLine();
        Console.WriteLine("Clone Symphony Messenger repositories?");
        Console.WriteLine();
        Console.WriteLine("  [Y] Yes");
        Console.WriteLine("  [N] No");
        Console.WriteLine();

        bool cloneDefault = PromptYesNo("Clone repositories?", defaultYes: true);

        var additional = new List<string>();

        if (cloneDefault)
        {
            Console.WriteLine();
            Console.WriteLine($"  Default: {SetupConfiguration.DefaultRepository}");

            while (true)
            {
                Console.WriteLine();
                bool addMore = PromptYesNo("Would you like to add another repository?", defaultYes: false);
                if (!addMore) break;

                Console.Write("Repository URL: ");
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
        Console.WriteLine();
        Console.WriteLine("=== Step 4 – Package Provider ===");
        Console.WriteLine();
        Console.WriteLine("Select installation provider");
        Console.WriteLine();
        Console.WriteLine("  1. Auto Detect");
        Console.WriteLine("  2. Winget");
        Console.WriteLine("  3. Chocolatey");
        Console.WriteLine();

        PackageProvider choice;
        while (true)
        {
            Console.Write("Choice: ");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": choice = PackageProvider.AutoDetect; goto done;
                case "2": choice = PackageProvider.Winget; goto done;
                case "3": choice = PackageProvider.Chocolatey; goto done;
                default:
                    Console.WriteLine("Please enter 1, 2, or 3.");
                    break;
            }
        }

        done:
        // Warn if Chocolatey is selected but not installed
        if (choice == PackageProvider.Chocolatey && !_packageProvider.IsChocolateyAvailable())
        {
            Console.WriteLine();
            Console.WriteLine("Chocolatey was not found.");
            Console.WriteLine();
            bool install = PromptYesNo("Install Chocolatey?", defaultYes: true);
            if (!install)
            {
                Console.WriteLine("Falling back to Winget.");
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
        Console.WriteLine();
        Console.WriteLine("=== Step 5 – Visual Studio ===");
        Console.WriteLine();
        Console.WriteLine("Install Visual Studio?");
        Console.WriteLine();
        Console.WriteLine("  1. Community");
        Console.WriteLine("  2. Professional");
        Console.WriteLine("  3. Skip");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Choice: ");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": return VisualStudioEdition.Community;
                case "2":
                    Console.WriteLine();
                    Console.Write("Enter Product Key (optional, press Enter to skip): ");
                    // Read but do not store in any variable that could be logged
                    ReadProductKeySecurely();
                    return VisualStudioEdition.Professional;
                case "3": return VisualStudioEdition.Skip;
                default:
                    Console.WriteLine("Please enter 1, 2, or 3.");
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
        Console.WriteLine();
        Console.WriteLine("=== Step 6 – JetBrains Rider ===");
        Console.WriteLine();
        return PromptYesNo("Install JetBrains Rider?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 7
    // -------------------------------------------------------------------------
    private bool Step7_DotNetFramework()
    {
        Console.WriteLine();
        Console.WriteLine("=== Step 7 – .NET Framework 4.8 ===");
        Console.WriteLine();

        if (_sysCheck.IsDotNetFramework48Installed())
        {
            Console.WriteLine(".NET Framework 4.8 is already installed.");
            return false; // no action needed
        }

        return PromptYesNo("Install .NET Framework 4.8?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 8
    // -------------------------------------------------------------------------
    private bool Step8_Git()
    {
        Console.WriteLine();
        Console.WriteLine("=== Step 8 – Git ===");
        Console.WriteLine();

        if (_git.IsGitInstalled())
        {
            Console.WriteLine("Git is already installed.");
            return false;
        }

        Console.WriteLine("Git was not detected.");
        Console.WriteLine();
        return PromptYesNo("Install Git?", defaultYes: true);
    }

    // -------------------------------------------------------------------------
    // Step 9
    // -------------------------------------------------------------------------
    private static bool Step9_Review(SetupConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("=== Step 9 – Review Configuration ===");
        Console.WriteLine();
        Console.WriteLine($"  Environment:       {config.Environment}");
        Console.WriteLine($"  Working Directory: {config.WorkingDirectory}");
        Console.WriteLine();
        Console.WriteLine("  Repositories:");
        if (config.CloneDefaultRepository)
            Console.WriteLine($"    ✓ {SetupConfiguration.DefaultRepository}");
        foreach (var repo in config.AdditionalRepositories)
            Console.WriteLine($"    ✓ {repo}");

        Console.WriteLine();
        Console.WriteLine("  Tools:");
        if (config.VisualStudioEdition != VisualStudioEdition.Skip)
            Console.WriteLine($"    ✓ Visual Studio {config.VisualStudioEdition}");
        if (config.InstallRider)
            Console.WriteLine("    ✓ JetBrains Rider");
        if (config.InstallDotNetFramework48)
            Console.WriteLine("    ✓ .NET Framework 4.8");
        if (config.InstallGit)
            Console.WriteLine("    ✓ Git");

        Console.WriteLine();
        Console.WriteLine("Proceed with installation?");
        Console.WriteLine();
        Console.WriteLine("  [Y] Install");
        Console.WriteLine("  [N] Cancel");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Choice [Y/N]: ");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (input == "Y" || input == "") return true;
            if (input == "N") return false;
            Console.WriteLine("Please enter Y or N.");
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
            Console.Write($"{prompt} {hint}: ");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(input))
                return defaultYes;

            if (input == "Y") return true;
            if (input == "N") return false;

            Console.WriteLine("Please enter Y or N.");
        }
    }

    private static void PrintBanner()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║       Developer Environment Bootstrapper         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }
}
