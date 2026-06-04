# DevBootstrapper

A **.NET 10** interactive console wizard that automates installation of a complete developer environment (Visual Studio, JetBrains Rider, .NET Framework 4.8, Git) using **winget** or **Chocolatey**, with optional Symphony Messenger repository cloning.

Supports **version selection** for Visual Studio (2019 / 2022) and JetBrains Rider (latest or specific), **one-click Automatic Install** from `bootstrapper.ini`, **silent / unattended installs**, and a fully extensible `bootstrapper.ini` configuration file — including a `[WingetPackageIds]` section to override package IDs without touching code.

---

## Screenshots

### Wizard – Automatic Install option at launch

![Setup Wizard](docs/screenshots/wizard.svg)

### Installation Dashboard – Live progress with Terminal.Gui

![Installation Dashboard](docs/screenshots/dashboard.svg)

### Completion Screen – Summary of installed tools

![Completion Screen](docs/screenshots/complete.svg)

---

## Features

- **Automatic Install at launch** – choose `[1] Automatic Install` at the very first prompt to install everything defined in `bootstrapper.ini` with no further prompts
- **11-step interactive wizard** – guided prompts from environment selection through tool version selection, silent-install toggle, and extra package configuration; followed by a live installation phase and a completion summary
- **VS version selection** – choose Visual Studio 2019 or 2022 (Community or Professional)
- **Rider version selection** – install the latest Rider release or pin to a specific version (e.g. `2024.3`)
- **Silent install mode** – suppresses interactive installer windows; ideal for CI or scripted builds
- **Extra winget packages** – add any number of additional software packages (e.g. Windows Terminal, PowerShell 7, Docker Desktop) via the wizard or `bootstrapper.ini`
- **`[WingetPackageIds]` section** – override the winget package ID for any known tool in `bootstrapper.ini` without touching the source code
- **`bootstrapper.ini` configuration file** – pre-populate wizard defaults or drive a fully unattended run with `--silent`
- **Vivid Terminal.Gui dashboard** – full-screen TUI with bright 16-colour schemes, a live progress bar, task list and scrolling log panel
- **Dual package providers** – winget (preferred) or Chocolatey with optional auto-install; auto-detect mode picks the best available provider
- **Secure product key input** – VS Professional key is read char-by-char, masked (`*`), and cleared from memory immediately; never reaches any log file
- **.NET Framework 4.8 detection** – checks `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` (release value ≥ 528040) before offering installation
- **Skip already-installed tools** – each task is checked before running, avoiding duplicate installs
- **Persistent log** – `%LOCALAPPDATA%\DevBootstrapper\Logs\install_<timestamp>.log`

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 / 11 | Registry-based checks require Windows |
| .NET 10 SDK | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| winget **or** Chocolatey | At least one must be present, or choose auto-install Chocolatey |

---

## Getting Started

```bash
# Clone the repository
git clone https://github.com/dotnetappdev/symp-check.git
cd symp-check

# Build
dotnet build src/DevBootstrapper/DevBootstrapper.csproj

# Run (requires Windows – uses winget / Chocolatey / registry APIs)
dotnet run --project src/DevBootstrapper/DevBootstrapper.csproj

# Unattended / silent mode – skips the wizard and uses bootstrapper.ini
dotnet run --project src/DevBootstrapper/DevBootstrapper.csproj -- --silent
```

> **Note:** The installer calls `winget` / `choco` and writes to the Windows registry. Run in an elevated terminal when installing software.

---

## Configuration File (`bootstrapper.ini`)

`bootstrapper.ini` lives next to the executable (or in the current working directory).  It pre-populates wizard defaults, enables **Automatic Install** at launch, and drives fully **unattended installation** via `--silent`.

```ini
; DevBootstrapper Configuration File
[General]
WorkingDirectory=C:\Work
PackageProvider=AutoDetect   ; AutoDetect | Winget | Chocolatey
SilentInstall=false

[VisualStudio]
Edition=Community            ; Community | Professional | Skip
Version=VS2022               ; VS2019 | VS2022

[Rider]
Install=true
Version=latest               ; "latest" or specific e.g. 2024.3

[Git]
Install=true

[DotNetFramework48]
Install=true

[AdditionalPackages]
; Add any number of extra winget package IDs
Package1=Microsoft.PowerShell
Package2=Microsoft.WindowsTerminal
Package3=Notepad++.Notepad++
Package4=Docker.DockerDesktop
Package5=Postman.Postman

[WingetPackageIds]
; Override the winget package ID used for each known tool.
; Remove a line to revert to the built-in default.
Git=Git.Git
DotNetFramework48=Microsoft.DotNet.Framework.DeveloperPack_4
Rider=JetBrains.Rider
VisualStudio2022Community=Microsoft.VisualStudio.2022.Community
VisualStudio2022Professional=Microsoft.VisualStudio.2022.Professional
VisualStudio2019Community=Microsoft.VisualStudio.2019.Community
VisualStudio2019Professional=Microsoft.VisualStudio.2019.Professional
```

After the wizard completes (or after Automatic Install), your choices are automatically saved back to `bootstrapper.ini` so the next run uses them as defaults.

### `[WingetPackageIds]` — overriding package IDs

Every known tool has a built-in winget package ID.  The `[WingetPackageIds]` section lets you change any of them without modifying source code — useful when an organisation mirrors packages or when a newer winget ID becomes available.

| Key | Default ID |
|---|---|
| `Git` | `Git.Git` |
| `DotNetFramework48` | `Microsoft.DotNet.Framework.DeveloperPack_4` |
| `Rider` | `JetBrains.Rider` |
| `VisualStudio2022Community` | `Microsoft.VisualStudio.2022.Community` |
| `VisualStudio2022Professional` | `Microsoft.VisualStudio.2022.Professional` |
| `VisualStudio2019Community` | `Microsoft.VisualStudio.2019.Community` |
| `VisualStudio2019Professional` | `Microsoft.VisualStudio.2019.Professional` |

### Finding winget package IDs

```powershell
winget search <name>          # e.g. winget search "notepad++"
```

Or browse [https://winget.run](https://winget.run) / [https://winstall.app](https://winstall.app).

---

## Automatic Install

At the very first prompt the wizard offers two choices:

```
  [1]  Automatic Install  — install all tools from bootstrapper.ini without prompts
  [2]  Interactive Setup  — configure step-by-step with the guided wizard
```

Choosing **`[1]`** immediately reads `bootstrapper.ini` (or uses built-in defaults when no file is present), prints a summary of what will be installed, and proceeds straight to the Terminal.Gui dashboard — no wizard steps required.

> **Tip:** Combine Automatic Install with `SilentInstall=true` in `[General]` for a completely hands-free run.

---

## Wizard Steps

| Step | Description |
|---|---|
| Launch | **Mode selection** — `[1]` Automatic Install (use bootstrapper.ini, no prompts) or `[2]` Interactive Setup |
| 1 | Select environment (Symphony Messenger or Custom) |
| 2 | Choose working directory (default `C:\Work`) |
| 3 | Repository setup – clone Symphony Messenger and/or additional repos |
| 4 | Select package provider (Auto / winget / Chocolatey) |
| 5 | Visual Studio edition (Community / Professional / Skip) **+ release year (2019 / 2022)** |
| 6 | JetBrains Rider (Yes / No) **+ version (latest or specific)** |
| 7 | .NET Framework 4.8 (auto-detected, skip if already installed) |
| 8 | Git (auto-detected, skip if already installed) |
| 9 | Silent install toggle (suppress installer UI) |
| 10 | Additional winget packages (add any extra software) |
| 11 | Review configuration and confirm |
| — | **Installation** – Terminal.Gui live dashboard (progress bar, task list, log) |
| — | **Completion** – console summary of installed tools and log path |

---

## Silent / Unattended Installation

Edit `bootstrapper.ini` to reflect your desired setup, then run:

```powershell
DevBootstrapper.exe --silent
```

The wizard is bypassed entirely and all packages are installed from the config file. `SilentInstall=true` additionally suppresses all package-installer windows.

---

## Project Structure

```
src/DevBootstrapper/
├── Models/
│   ├── InstallTask.cs            – task + InstallStatus enum
│   └── SetupConfiguration.cs     – all wizard state (incl. VS version, Rider version, silent flag)
├── Wizard/
│   ├── SetupWizard.cs            – 11-step interactive prompts
│   └── TaskListBuilder.cs        – builds task list from config
├── Services/
│   ├── PackageProviderService.cs – winget / choco detection & invocation
│   ├── GitService.cs             – git detection & clone
│   ├── SystemCheckService.cs     – registry-based .NET Framework 4.8 + VS version detection
│   ├── ConfigurationFileService.cs – bootstrapper.ini reader / writer
│   └── LogService.cs             – timestamped log file writer
├── Installers/
│   └── InstallOrchestrator.cs    – async orchestration, version-aware package IDs
├── UI/
│   ├── ProgressDashboard.cs      – Terminal.Gui TUI (progress bar, task list, log)
│   └── CompletionScreen.cs       – console completion summary
└── Program.cs                    – entry point (loads ini, handles --silent flag)

bootstrapper.ini                  – configuration file template
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Terminal.Gui` | 2.0.0 | Full-screen TUI progress dashboard |

---

## Colour Scheme

The Terminal.Gui dashboard uses a vivid 16-colour palette (matching the style of Terminal.Gui's own demo applications):

| Panel | Colour |
|---|---|
| Title banner | Bright Yellow on Black |
| Overall Progress frame | Bright Green on Black |
| Progress bar | Bright Green fill |
| Current task label | Bright Cyan on Black |
| Tasks panel | Bright Cyan borders, White text |
| Live Log panel | Bright Magenta borders, Bright Yellow text |

The console wizard steps and completion screen use matching bright `ConsoleColor` values (Cyan for step headers, Yellow for choices and warnings, Green for checkmarks).

---

## Security Notes

- Product keys entered in Step 5 are **never logged** – the value is held in a `StringBuilder` that is explicitly cleared (`.Clear()`) immediately after use and the result is not stored in any field or passed to any log method.
- No credentials or keys are written to disk at any point.
- `bootstrapper.ini` is plain text – do **not** add product keys or credentials to it.

---

## License

This project is part of the [symp-check](https://github.com/dotnetappdev/symp-check) repository.
