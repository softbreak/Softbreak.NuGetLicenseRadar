# CI acceptance: test a packed .NET tool on each runtime without changing global tools.
[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts/nuget',
    [string]$Version = '0.2.0'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$packageDir = [IO.Path]::GetFullPath((Join-Path $root $PackageDirectory))
$packagePath = Join-Path $packageDir "Softbreak.NuGetLicenseRadar.$Version.nupkg"
$frameworks = @('net6.0', 'net7.0', 'net8.0', 'net9.0', 'net10.0')

if (-not (Test-Path -LiteralPath $packagePath)) { throw "PACKAGE_NOT_FOUND: $packagePath" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    foreach ($framework in $frameworks) {
        $settings = "tools/$framework/any/DotnetToolSettings.xml"
        if (-not @($archive.Entries | Where-Object { $_.FullName -eq $settings }).Count) {
            throw "TOOL_SETTINGS_MISSING: $settings"
        }
        $turkishSatellite = "tools/$framework/any/tr/Softbreak.LicenseRadar.Cli.resources.dll"
        if (-not @($archive.Entries | Where-Object { $_.FullName -eq $turkishSatellite }).Count) {
            throw "TURKISH_RESOURCE_MISSING: $turkishSatellite"
        }
        Write-Host "PACKAGE SETTINGS: PASS ($framework)"
        Write-Host "PACKAGE TURKISH RESOURCES: PASS ($framework)"
    }
}
finally { $archive.Dispose() }

$runtimes = @(& dotnet --list-runtimes)
if ($LASTEXITCODE -ne 0) { throw 'DOTNET_RUNTIMES_QUERY_FAILED' }

$work = Join-Path $env:TEMP ('license-radar-tfm-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null
try {
    $env:NO_COLOR = '1'
    foreach ($framework in $frameworks) {
        $major = $framework.Replace('net', '').Split('.')[0]
        if (-not (@($runtimes | Where-Object { $_ -match "^Microsoft\.NETCore\.App $major\." }).Count)) {
            throw "RUNTIME_NOT_INSTALLED: $framework"
        }
        $toolDir = Join-Path $work $framework
        & dotnet tool install Softbreak.NuGetLicenseRadar --tool-path $toolDir --version $Version --framework $framework --add-source $packageDir
        if ($LASTEXITCODE -ne 0) { throw "TOOL_INSTALL_FAILED: $framework" }

        $exe = Join-Path $toolDir 'license-radar.exe'
        if (-not (Test-Path -LiteralPath $exe)) { throw "TOOL_EXECUTABLE_MISSING: $framework" }
        & $exe --help | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "TOOL_HELP_FAILED: $framework" }

        $fixtureDir = Join-Path $work ("fixture-" + $framework)
        New-Item -ItemType Directory -Force -Path $fixtureDir | Out-Null
        $lock = Join-Path $fixtureDir 'packages.lock.json'
        $lockJson = '{"version":2,"dependencies":{"' + $framework + '":{"Sample.Project":{"type":"Project"}}}}'
        [IO.File]::WriteAllText($lock, $lockJson, [Text.UTF8Encoding]::new($false))
        $output = & $exe scan $lock
        if ($LASTEXITCODE -ne 0 -or -not (($output -join [Environment]::NewLine) -match '(Total|Toplam): 0\b')) {
            throw "TOOL_SCAN_FAILED: $framework"
        }
        Write-Host "FRAMEWORK_SMOKE: PASS ($framework)"
    }
    Write-Host 'RESULT: ALL_FRAMEWORKS_VERIFIED'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
