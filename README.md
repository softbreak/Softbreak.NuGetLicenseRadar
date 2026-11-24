# Softbreak NuGet License Radar

[![CI](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/workflows/ci.yml/badge.svg)](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Softbreak.NuGetLicenseRadar.svg)](https://www.nuget.org/packages/Softbreak.NuGetLicenseRadar)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/LICENSE)

**Identify NuGet dependencies that need a closer look at their license terms.**

Softbreak NuGet License Radar is an open-source .NET CLI tool that scans a project's resolved NuGet dependencies, reads package license declarations and available license files, and highlights evidence worth reviewing. It reports findings; it does **not** certify legal compliance or determine whether a commercial license must be purchased.

## Features

- Discover dependencies in .NET projects and directory trees, including direct and transitive packages.
- Prefer `packages.lock.json`, falling back to `obj/project.assets.json`.
- Query package metadata and embedded license files from [NuGet.org](https://www.nuget.org/).
- Highlight license-text signals such as commercial license requirements, reciprocal licenses, and alternative agreements.
- Show a concise terminal summary, detailed output with `--verbose`, or a machine-readable JSON report.
- Scan without changing project files, running `dotnet restore`, or uploading source code.

## Installation

Install the latest **published** version from NuGet.org:

```bash
dotnet tool install --global Softbreak.NuGetLicenseRadar
```

Already installed? Update with:

```bash
dotnet tool update --global Softbreak.NuGetLicenseRadar
```

Run `license-radar --help` to see available options. If the command is not recognized after installation, restart your terminal and ensure the [.NET global tools directory](https://learn.microsoft.com/dotnet/core/tools/global-tools#install-a-global-tool) is on `PATH`.

### Runtime compatibility and release status

| Version | Availability | Tool runtime |
| --- | --- | --- |
| **0.1.1** | Published on NuGet.org | .NET 10 |
| **0.2.0** | Merged into `main`; **not yet published** | .NET 6, 7, 8, 9, and 10 |

The `main` branch and the NuGet.org package **do not currently represent the same release**. Until 0.2.0 is published, installing from NuGet.org does not provide .NET 6–9 runtime support. The 0.2.0 release candidate has passed the [five-framework CI acceptance suite](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/runs/45419708989) and a real-project scan.

The tool can inspect dependency files for .NET 6–10 projects regardless of the runtime used to run the tool. You need a compatible .NET SDK to install a global tool and a suitable runtime to execute it. **.NET 6–9 are out of Microsoft support** as of November 2026; compatibility here does not extend their security support. Use .NET 10 LTS for new projects.

## Quick start

Navigate to your .NET project or solution directory and run:

```bash
license-radar scan .
```

You can also target a project file or another directory:

```bash
license-radar scan ./src/MyApp/MyApp.csproj
license-radar scan ./src
```

### Command reference

| Command | Description |
| --- | --- |
| `license-radar scan .` | Scan the current directory. |
| `license-radar scan <path>` | Scan a project, solution location, lock file, or directory. |
| `license-radar scan . --verbose` | List all packages and additional evidence. |
| `license-radar scan . --output report.json` | Write a versioned JSON report. |
| `license-radar --help` | Display command-line help. |

The `--output` option **will not overwrite an existing file**. The current JSON schema is version 1.

## Understanding the results

| Status | Meaning |
| --- | --- |
| **ReviewRequired** | A license file or text signal warrants a manual review. This does not necessarily mean payment is required. |
| **Unknown** | The license declaration could not be established or retrieved. This does not imply that the package is paid or noncompliant. |
| **MetadataOnly** | License metadata was found, but usage rights have not been independently verified. |

For example, a package's license text may mention a paid commercial license outside specific permitted uses. License Radar reports that wording as **evidence to review**, not a purchase instruction.

## How it works

1. Discover project files and their resolved dependency records from `packages.lock.json` or `obj/project.assets.json`.
2. Deduplicate packages by ID, version, and target framework.
3. Read available NuGet license declarations and embedded license files.
4. Evaluate supported text signals and report items that require review or could not be verified.

**Important limitations:**

- The scanner does not interpret every license, external vendor agreement, or commercial policy.
- An `Unknown` result may reflect missing metadata or network failures.
- `project.assets.json` reflects the last restore; update it yourself if project dependencies have changed.
- When targeting a `.sln` or `.slnx` file, the tool scans its directory tree rather than parsing solution membership.
- The tool is not a vulnerability scanner, a policy-enforcement gate, or a substitute for legal advice.

NuGet requests go to `api.nuget.org` for package metadata and license content. **Project source code is not uploaded.**

## Development

Clone the repository and use the **.NET 10 SDK** to build from source:

```bash
git clone https://github.com/softbreak/Softbreak.NuGetLicenseRadar.git
cd Softbreak.NuGetLicenseRadar
dotnet restore Softbreak.NuGetLicenseRadar.slnx
dotnet build Softbreak.NuGetLicenseRadar.slnx -c Release --no-restore
dotnet test Softbreak.NuGetLicenseRadar.slnx -c Release --no-build --no-restore
```

Run the CLI from source:

```bash
dotnet run --project Softbreak.LicenseRadar.Cli --framework net10.0 -- scan .
```

The [GitHub Actions workflow](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/.github/workflows/ci.yml) builds, tests, packs, and validates isolated tool installation for .NET 6–10. On Windows, `scripts/Test-Release.ps1` checks the release candidate without modifying globally installed tools or publishing to NuGet.org.

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please [open an issue](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/issues) to discuss larger changes before implementation. Run the relevant tests and describe the intended behavior in your pull request.

## License

Distributed under the **MIT License**. See [LICENSE](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/LICENSE) for details.

The MIT license applies to **this tool**, not to the third-party NuGet packages it scans.
