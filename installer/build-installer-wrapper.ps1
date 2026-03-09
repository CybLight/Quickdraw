param(
    [ValidateSet("all", "x64", "x86")]
    [string]$Profile = "all",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $scriptDir "build-installer.ps1"

$runtimes = switch ($Profile) {
    "x64" { @("win-x64") }
    "x86" { @("win-x86") }
    default { @("win-x64", "win-x86") }
}

& $buildScript -Configuration $Configuration -Runtimes $runtimes
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
