# .NET Tool 0.1.0 yayın adayı için izole, global kuruluma dokunmayan Windows ön doğrulaması.
# Yalnızca dotnet test/pack ve geçici --tool-path install yapar. NuGet.org'a yüklemez.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repo 'Softbreak.NuGetLicenseRadar.slnx'
$project = Join-Path $repo 'Softbreak.LicenseRadar.Cli\Softbreak.LicenseRadar.Cli.csproj'
$toolId = 'Softbreak.NuGetLicenseRadar'
$version = '0.1.0'
$work = Join-Path $repo ('.local-test\release-' + [guid]::NewGuid().ToString('N'))
$packageDir = Join-Path $work 'packages'
$toolDir = Join-Path $work 'tool'
$package = Join-Path $packageDir ('{0}.{1}.nupkg' -f $toolId, $version)

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('DOTNET_FAILED exit={0}: dotnet {1}' -f $LASTEXITCODE, ($Arguments -join ' '))
    }
}

function Get-MetadataNodeText {
    param([System.Xml.XmlNode]$Metadata, [string]$Name)
    $node = $Metadata.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return '' }
    return $node.InnerText
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET CLI bulunamadı. .NET 10 SDK kurun: https://dotnet.microsoft.com/download/dotnet/10.0'
}
$sdkList = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not (@($sdkList | Select-String '^10\.')).Count) {
    throw '.NET 10 SDK bulunamadı.'
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Write-Host '[1/4] Release testleri...' -ForegroundColor Cyan
Invoke-DotNet test $solution '--configuration' 'Release'

Write-Host '[2/4] 0.1.0 NuGet Tool paketleniyor...' -ForegroundColor Cyan
Invoke-DotNet pack $project '--configuration' 'Release' '--output' $packageDir '--no-restore'
if (-not (Test-Path -LiteralPath $package)) {
    throw ('PACKAGE_NOT_FOUND: {0}' -f $package)
}

Write-Host '[3/4] nupkg manifest, README ve MIT lisansı kontrol ediliyor...' -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -match '\.nuspec$' })
    if ($nuspecEntries.Count -ne 1) { throw 'NUSPEC_COUNT_UNEXPECTED' }

    $entryStream = $nuspecEntries[0].Open()
    $reader = New-Object System.IO.StreamReader($entryStream)
    try { [xml]$spec = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
    $meta = $spec.SelectSingleNode("//*[local-name()='metadata']")
    if ($null -eq $meta) { throw 'NUSPEC_METADATA_MISSING' }
    if ((Get-MetadataNodeText $meta 'id') -ne $toolId) { throw 'PACKAGE_ID_MISMATCH' }
    if ((Get-MetadataNodeText $meta 'version') -ne $version) { throw 'PACKAGE_VERSION_MISMATCH' }
    if ((Get-MetadataNodeText $meta 'readme') -ne 'README.md') { throw 'PACKAGE_README_MISMATCH' }
    $license = $meta.SelectSingleNode("*[local-name()='license']")
    if ($null -eq $license -or $license.InnerText -ne 'MIT' -or $license.GetAttribute('type') -ne 'expression') {
        throw 'PACKAGE_LICENSE_MISMATCH'
    }
    $packageType = $meta.SelectSingleNode("*[local-name()='packageTypes']/*[local-name()='packageType']")
    if ($null -eq $packageType -or $packageType.GetAttribute('name') -ne 'DotnetTool') {
        throw 'PACKAGE_TYPE_NOT_DOTNET_TOOL'
    }
    if (-not (@($archive.Entries | Where-Object { $_.FullName -eq 'README.md' }).Count)) {
        throw 'PACKAGE_README_NOT_EMBEDDED'
    }
}
finally {
    $archive.Dispose()
}

Write-Host '[4/4] Global kurulum etkilenmeden izole tool kurulumu ve --help testi...' -ForegroundColor Cyan
Invoke-DotNet tool install $toolId '--tool-path' $toolDir '--version' $version '--add-source' $packageDir
$toolExe = Join-Path $toolDir 'license-radar.exe'
if (-not (Test-Path -LiteralPath $toolExe)) {
    throw ('ISOLATED_TOOL_NOT_FOUND: {0}' -f $toolExe)
}
& $toolExe --help | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'HELP_COMMAND_FAILED' }

$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
$summary = @(
    '=== LICENSE RADAR 0.1.0 RELEASE CANDIDATE ==='
    'RELEASE_TESTS: PASS'
    'PACK: PASS'
    'METADATA: PASS'
    'ISOLATED_INSTALL: PASS'
    'GLOBAL_TOOL_MODIFIED: NO'
    'NUGET_ORG_PUBLISHED: NO'
    'MANUAL_REAL_PROJECT_SCAN: PENDING'
    ('NUPKG: ' + $package)
    ('SHA256: ' + $hash)
    'RESULT: READY_FOR_MANUAL_ACCEPTANCE'
)
$summary | Out-Host
try { $summary | Out-String | Set-Clipboard } catch { }
