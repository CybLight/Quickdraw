param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputMsix = "PriorityManagerX.Sparse.msix",
    # Каталог установки Win32‑приложения (куда Inno ставит PMX)
    [string]$ExternalInstallDir = "C:\Program Files\Priority Manager X",
    # Путь к MakeAppx.exe (из Windows SDK). Если пусто — пытаемся угадать.
    [string]$MakeAppxPath = "",
    # Путь к pfx‑сертификату и его пароль (для подписи пакета).
    [string]$PfxPath = "",
    [SecureString]$PfxPassword,
    # Авто‑инкремент версии в Package.appxmanifest перед сборкой.
    [bool]$AutoIncrementVersion = $true
)

$ErrorActionPreference = "Stop"

function Resolve-MakeAppx {
    param([string]$Path)
    if ($Path -and (Test-Path $Path)) { return $Path }

    $sdkRoots = @(
        "$Env:ProgramFiles (x86)\Windows Kits\10\bin",
        "$Env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { Test-Path $_ }

    $archOrder = if ([Environment]::Is64BitProcess) {
        @("x64", "x86", "arm64", "arm")
    } else {
        @("x86", "x64", "arm64", "arm")
    }

    foreach ($root in $sdkRoots) {
        $versionDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                [PSCustomObject]@{
                    Version = [Version]$_.Name
                    Dir     = $_
                }
            } catch {
                $null
            }
        } | Where-Object { $_ } | Sort-Object Version -Descending

        foreach ($versionDir in $versionDirs) {
            foreach ($arch in $archOrder) {
                $candidate = Join-Path $versionDir.Dir.FullName "$arch\makeappx.exe"
                if (Test-Path $candidate) { return $candidate }
            }
        }
    }

    throw "MakeAppx.exe не найден. Установи Windows 10/11 SDK и укажи путь через параметр -MakeAppxPath."
}

function Resolve-SignTool {
    $sdkRoots = @(
        "$Env:ProgramFiles (x86)\Windows Kits\10\bin",
        "$Env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { Test-Path $_ }

    $archOrder = if ([Environment]::Is64BitProcess) {
        @("x64", "x86", "arm64", "arm")
    } else {
        @("x86", "x64", "arm64", "arm")
    }

    foreach ($root in $sdkRoots) {
        $versionDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                [PSCustomObject]@{
                    Version = [Version]$_.Name
                    Dir     = $_
                }
            } catch {
                $null
            }
        } | Where-Object { $_ } | Sort-Object Version -Descending

        foreach ($versionDir in $versionDirs) {
            foreach ($arch in $archOrder) {
                $candidate = Join-Path $versionDir.Dir.FullName "$arch\signtool.exe"
                if (Test-Path $candidate) { return $candidate }
            }
        }
    }

    throw "signtool.exe не найден. Установи Windows SDK или подпиши пакет вручную."
}

function Update-ManifestVersion {
    param([string]$ManifestPath)

    [xml]$xml = Get-Content -Path $ManifestPath
    $identity = $xml.Package.Identity
    if (-not $identity) {
        throw "Identity node not found in manifest: $ManifestPath"
    }

    $current = [Version]$identity.Version
    $major = [int]$current.Major
    $minor = [int]$current.Minor
    $build = [int]$current.Build
    $revision = [int]$current.Revision

    $revision++
    if ($revision -gt 65535) {
        $revision = 0
        $build++
        if ($build -gt 65535) {
            throw "Version overflow. Increase Major/Minor manually."
        }
    }

    $next = "$major.$minor.$build.$revision"
    $identity.Version = $next
    $xml.Save($ManifestPath)
    return $next
}

function Convert-SecureStringToPlainText {
    param([SecureString]$Value)

    if (-not $Value) {
        return ""
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

$root = Split-Path -Parent $PSScriptRoot
$sparseDir = Join-Path $PSScriptRoot "layout"

if (Test-Path $sparseDir) {
    Remove-Item $sparseDir -Recurse -Force
}

New-Item -ItemType Directory -Path $sparseDir | Out-Null

# Копируем manifest
$manifestPath = Join-Path $PSScriptRoot "Package.appxmanifest"
if ($AutoIncrementVersion) {
    $nextVersion = Update-ManifestVersion -ManifestPath $manifestPath
    Write-Host ("Manifest version incremented to: " + $nextVersion)
}
Copy-Item $manifestPath (Join-Path $sparseDir "AppxManifest.xml")

# Копируем обязательные assets из проекта в пакет
$assetsSourceDir = Join-Path $root "assets"
$assetsTargetDir = Join-Path $sparseDir "Assets"
New-Item -ItemType Directory -Path $assetsTargetDir -Force | Out-Null
Copy-Item (Join-Path $assetsSourceDir "PmX.png") (Join-Path $assetsTargetDir "PmX.png") -Force

# Копируем COM-host и exe в пакет (относительные пути должны совпасть с manifest)
$publishDir = Join-Path $root "bin\$Configuration\net8.0-windows\$Runtime\publish"
$binDir = Join-Path $root "bin\$Configuration\net8.0-windows"
$shellDir = Join-Path $root "ShellExtension\bin\$Configuration\net8.0-windows"

$pmxExePath = Join-Path $publishDir "PriorityManagerX.exe"
if (-not (Test-Path $pmxExePath)) {
    $pmxExePath = Join-Path $binDir "PriorityManagerX.exe"
}
if (-not (Test-Path $pmxExePath)) {
    throw "PriorityManagerX.exe not found. Checked: '$publishDir' and '$binDir'."
}

New-Item -ItemType Directory -Path (Join-Path $sparseDir "PriorityManagerX") -Force | Out-Null
Copy-Item $pmxExePath (Join-Path $sparseDir "PriorityManagerX\") -Force

Copy-Item (Join-Path $shellDir "PriorityManagerX.ShellExtension.comhost.dll") (Join-Path $sparseDir "PriorityManagerX.ShellExtension.comhost.dll") -Force
Copy-Item (Join-Path $shellDir "PriorityManagerX.ShellExtension.dll") (Join-Path $sparseDir "PriorityManagerX.ShellExtension.dll") -Force
Copy-Item (Join-Path $shellDir "PriorityManagerX.ShellExtension.deps.json") (Join-Path $sparseDir "PriorityManagerX.ShellExtension.deps.json") -Force
Copy-Item (Join-Path $shellDir "PriorityManagerX.ShellExtension.runtimeconfig.json") (Join-Path $sparseDir "PriorityManagerX.ShellExtension.runtimeconfig.json") -Force

$clsidMapFromObj = Join-Path $root "ShellExtension\obj\$Configuration\net8.0-windows\PriorityManagerX.ShellExtension.clsidmap"
if (Test-Path $clsidMapFromObj) {
    Copy-Item $clsidMapFromObj (Join-Path $sparseDir "PriorityManagerX.ShellExtension.clsidmap") -Force
}

# Собираем MSIX
$make = Resolve-MakeAppx -Path $MakeAppxPath
Write-Host ("Using MakeAppx: " + $make)
Push-Location $sparseDir
$makeArgs = @("pack", "/d", $sparseDir, "/p", (Join-Path $root $OutputMsix), "/o")
& $make @makeArgs
$makeExitCode = $LASTEXITCODE
Pop-Location
if ($makeExitCode -ne 0) {
    throw "MakeAppx failed with exit code $makeExitCode."
}

if ($PfxPath -and (Test-Path $PfxPath)) {
    try {
        $null = Get-PfxData -FilePath $PfxPath -Password $PfxPassword
    } catch {
        throw "PFX validation failed. Check PFX path and password."
    }

    $signtool = Resolve-SignTool
    $plainPfxPassword = Convert-SecureStringToPlainText -Value $PfxPassword
    & $signtool sign /f $PfxPath /p $plainPfxPassword /fd SHA256 /a (Join-Path $root $OutputMsix)
    $plainPfxPassword = $null
    $signExitCode = $LASTEXITCODE
    if ($signExitCode -ne 0) {
        throw "SignTool failed with exit code $signExitCode. Check PFX password and Publisher match."
    }
} else {
    Write-Host "MSIX package created without signing. You need to sign it before installing on a normal system."
}

Write-Host ("Done. MSIX path: " + (Join-Path $root $OutputMsix))

