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

    /// <summary>
    /// Runs all wizard steps and returns the completed configuration.
    /// If <paramref name="initialConfig"/> is provided (e.g. loaded from <c>bootstrapper.ini</c>)
    /// it is used as the starting state and its values become the wizard defaults.
    /// </summary>
    public SetupConfiguration Run(SetupConfiguration? initialConfig = null)
    {
        PrintBanner();

        var config = initialConfig ?? new SetupConfiguration();

        // Step 1 – Environment
        config.Environment = Step1_SelectEnvironment(config.Environment);

        // Step 2 – Working directory
        config.WorkingDirectory = Step2_WorkingDirectory(config.WorkingDirectory);

        // Step 3 – Repository setup
        if (config.Environment == EnvironmentType.SymphonyMessenger)
        {
            (config.CloneDefaultRepository, config.AdditionalRepositories) =
                Step3_RepositorySetup(config.CloneDefaultRepository);
        }

        // Step 4 – Package provider
        config.PackageProvider = Step4_PackageProvider(config.PackageProvider);

        // Step 5 – Visual Studio edition + version
        (config.VisualStudioEdition, config.VisualStudioVersion) =
            Step5_VisualStudio(config.VisualStudioEdition, config.VisualStudioVersion);

        // Step 6 – JetBrains Rider + version
        (config.InstallRider, config.RiderVersion) =
            Step6_JetBrainsRider(config.InstallRider, config.RiderVersion);

        // Step 7 – .NET Framework 4.8
        config.InstallDotNetFramework48 = Step7_DotNetFramework(config.InstallDotNetFramework48);

        // Step 8 – Git
        config.InstallGit = Step8_Git(config.InstallGit);

        // Step 9 – Silent install
        config.SilentInstall = Step9_SilentInstall(config.SilentInstall);

        // Step 10 – Additional winget packages
        config.AdditionalWingetPackages = Step10_AdditionalPackages(config.AdditionalWingetPackages);

        // Step 11 – Review
        bool proceed = Step11_Review(config);
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
    private static EnvironmentType Step1_SelectEnvironment(EnvironmentType current)
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
                case "":
                    return current; // accept config-file default
                default:
                    WriteWarning("Please enter 1 or 2.");
                    break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step 2
    // -------------------------------------------------------------------------
    private static string Step2_WorkingDirectory(string current)
    {
        PrintStepHeader(2, "Working Directory");
        WriteColored("Default: ", ConsoleColor.Gray);
        WriteColored(current, ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine();
        WriteColored("Use default?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("Y", $"Yes – use {current}");
        WriteChoice("N", "No  – enter custom path");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice [Y/N]");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (input == "Y" || input == "")
                return current;

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
    private static (bool cloneDefault, List<string> additional) Step3_RepositorySetup(bool currentClone)
    {
        PrintStepHeader(3, "Repository Setup");
        WriteColored("Clone Symphony Messenger repositories?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("Y", "Yes");
        WriteChoice("N", "No");
        Console.WriteLine();

        bool cloneDefault = PromptYesNo("Clone repositories?", defaultYes: currentClone);

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
    private PackageProvider Step4_PackageProvider(PackageProvider current)
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
                case "":  choice = current; goto done;
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
    // Step 5 – Visual Studio edition + version
    // -------------------------------------------------------------------------
    private static (VisualStudioEdition edition, VisualStudioVersion version) Step5_VisualStudio(
        VisualStudioEdition currentEdition, VisualStudioVersion currentVersion)
    {
        PrintStepHeader(5, "Visual Studio");
        WriteColored("Install Visual Studio?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", "Community");
        WriteChoice("2", "Professional");
        WriteChoice("3", "Skip");
        Console.WriteLine();

        VisualStudioEdition edition;
        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": edition = VisualStudioEdition.Community; goto editionDone;
                case "2":
                    Console.WriteLine();
                    WritePrompt("Enter Product Key (optional, press Enter to skip)");
                    ReadProductKeySecurely();
                    edition = VisualStudioEdition.Professional;
                    goto editionDone;
                case "3": return (VisualStudioEdition.Skip, currentVersion);
                case "":  edition = currentEdition; goto editionDone;
                default:
                    WriteWarning("Please enter 1, 2, or 3.");
                    break;
            }
        }

        editionDone:
        // Ask for VS version
        VisualStudioVersion version = Step5b_VisualStudioVersion(currentVersion);
        return (edition, version);
    }

    private static VisualStudioVersion Step5b_VisualStudioVersion(VisualStudioVersion current)
    {
        Console.WriteLine();
        WriteColored("  Which Visual Studio release year?", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", "2019");
        WriteChoice("2", "2022  (recommended)");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1": return VisualStudioVersion.VS2019;
                case "2": return VisualStudioVersion.VS2022;
                case "":  return current;
                default:
                    WriteWarning("Please enter 1 or 2.");
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
    // Step 6 – JetBrains Rider + version
    // -------------------------------------------------------------------------
    private static (bool install, string version) Step6_JetBrainsRider(bool currentInstall, string currentVersion)
    {
        PrintStepHeader(6, "JetBrains Rider");
        Console.WriteLine();
        bool install = PromptYesNo("Install JetBrains Rider?", defaultYes: currentInstall);

        if (!install)
            return (false, currentVersion);

        // Ask for version
        Console.WriteLine();
        WriteColored("  Rider version:", ConsoleColor.White);
        Console.WriteLine();
        WriteChoice("1", $"Latest  (recommended)");
        WriteChoice("2", "Specific version  (e.g. 2024.1)");
        Console.WriteLine();

        while (true)
        {
            WritePrompt("Choice");
            string? input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                case "":
                    return (true, "latest");
                case "2":
                    Console.WriteLine();
                    WritePrompt("Enter version (e.g. 2024.1)");
                    string? ver = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(ver))
                        return (true, ver);
                    WriteWarning("Version cannot be empty – using latest.");
                    return (true, "latest");
                default:
                    WriteWarning("Please enter 1 or 2.");
                    break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step 7
    // -------------------------------------------------------------------------
    private bool Step7_DotNetFramework(bool current)
    {
        PrintStepHeader(7, ".NET Framework 4.8");

        if (_sysCheck.IsDotNetFramework48Installed())
        {
            WriteColored("  ✓  .NET Framework 4.8 is already installed.", ConsoleColor.Green);
            Console.WriteLine();
            return false; // no action needed
        }

        return PromptYesNo("Install .NET Framework 4.8?", defaultYes: current);
    }

    // -------------------------------------------------------------------------
    // Step 8
    // -------------------------------------------------------------------------
    private bool Step8_Git(bool current)
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
        return PromptYesNo("Install Git?", defaultYes: current);
    }

    // -------------------------------------------------------------------------
    // Step 9 – Silent install
    // -------------------------------------------------------------------------
    private static bool Step9_SilentInstall(bool current)
    {
        PrintStepHeader(9, "Silent Install");
        WriteColored("Run package installers in silent / non-interactive mode?", ConsoleColor.White);
        Console.WriteLine();
        WriteColored("  When enabled, installer windows and progress UI are suppressed.", ConsoleColor.Gray);
        Console.WriteLine();
        Console.WriteLine();
        return PromptYesNo("Silent install?", defaultYes: current);
    }

    // -------------------------------------------------------------------------
    // Step 10 – Additional winget packages
    // -------------------------------------------------------------------------
    private static List<string> Step10_AdditionalPackages(List<string> current)
    {
        PrintStepHeader(10, "Additional Packages");
        WriteColored("Add extra software to install via winget.", ConsoleColor.White);
        Console.WriteLine();

        var packages = new List<string>(current);

        if (packages.Count > 0)
        {
            WriteColored("  Already configured:", ConsoleColor.Gray);
            Console.WriteLine();
            foreach (string p in packages)
            {
                WriteColored("    • ", ConsoleColor.Yellow);
                WriteColored(p, ConsoleColor.Cyan);
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        while (true)
        {
            bool addMore = PromptYesNo("Add a winget package ID?", defaultYes: false);
            if (!addMore) break;

            Console.WriteLine();
            WriteColored("  Find IDs at: winget search <name>  or  https://winget.run", ConsoleColor.DarkGray);
            Console.WriteLine();
            WritePrompt("Winget Package ID (e.g. Microsoft.PowerShell)");
            string? id = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(id))
            {
                packages.Add(id);
                WriteColored($"  ✓  Added: ", ConsoleColor.Green);
                WriteColored(id, ConsoleColor.Cyan);
                Console.WriteLine();
            }
            else
            {
                WriteWarning("Package ID cannot be empty.");
            }
        }

        return packages;
    }

    // -------------------------------------------------------------------------
    // Step 11 – Review
    // -------------------------------------------------------------------------
    private static bool Step11_Review(SetupConfiguration config)
    {
        PrintStepHeader(11, "Review Configuration");

        WriteColored($"  Environment:       ", ConsoleColor.Gray);
        WriteColored(config.Environment.ToString(), ConsoleColor.Cyan);
        Console.WriteLine();
        WriteColored($"  Working Directory: ", ConsoleColor.Gray);
        WriteColored(config.WorkingDirectory, ConsoleColor.Cyan);
        Console.WriteLine();
        WriteColored($"  Silent Install:    ", ConsoleColor.Gray);
        WriteColored(config.SilentInstall ? "Yes" : "No", ConsoleColor.Cyan);
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
            Console.WriteLine($"Visual Studio {config.VisualStudioEdition} {config.VisualStudioVersion.ToYear()}");
        }
        if (config.InstallRider)
        {
            WriteColored("    ✓ ", ConsoleColor.Green);
            string riderDesc = config.RiderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase)
                ? "JetBrains Rider (latest)"
                : $"JetBrains Rider {config.RiderVersion}";
            Console.WriteLine(riderDesc);
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

        if (config.AdditionalWingetPackages.Count > 0)
        {
            Console.WriteLine();
            WriteColored("  Additional Packages:", ConsoleColor.White);
            Console.WriteLine();
            foreach (string pkg in config.AdditionalWingetPackages)
            {
                WriteColored("    ✓ ", ConsoleColor.Green);
                Console.WriteLine(pkg);
            }
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

