param(
    [string]$Configuration = "Release",
    [string[]]$Runtimes = @("win-x64", "win-x86"),
    [bool]$SelfContained = $false,
    [string]$SparsePfxPath = "",
    [SecureString]$SparsePfxPassword,
    [bool]$BuildSparsePackage = $true
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

$shellBuildDir = Join-Path $repoRoot "ShellExtension\bin\$Configuration\net8.0-windows"
$shellObjDir = Join-Path $repoRoot "ShellExtension\obj\$Configuration\net8.0-windows"
$stagingDir = Join-Path $scriptDir "publish"
$offlineRuntimeDir = Join-Path $scriptDir "offline-runtime"
$issFile = Join-Path $scriptDir "PriorityManagerX.iss"
$sparseBuildScript = Join-Path $repoRoot "SparsePackage\build-sparse-package.ps1"
$sparseInstallScript = Join-Path $repoRoot "SparsePackage\install-sparse-package.ps1"
$sparseUninstallScript = Join-Path $repoRoot "SparsePackage\uninstall-sparse-package.ps1"
$sparseMsixPath = Join-Path $repoRoot "PriorityManagerX.Sparse.msix"

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

Write-Host "[1/6] Prepare installer build..."
dotnet build (Join-Path $repoRoot "ShellExtension\PriorityManagerX.ShellExtension.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$shellFiles = @(
    "PriorityManagerX.ShellExtension.dll",
    "PriorityManagerX.ShellExtension.comhost.dll",
    "PriorityManagerX.ShellExtension.deps.json",
    "PriorityManagerX.ShellExtension.runtimeconfig.json",
    "PriorityManagerX.ShellExtension.clsidmap"
)

Write-Host "[2/6] Locate Inno Setup compiler (ISCC.exe)..."
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $isccPath) {
    throw "Inno Setup 6 is not installed. Install it from https://jrsoftware.org/isinfo.php"
}

if ($BuildSparsePackage -and [string]::IsNullOrWhiteSpace($SparsePfxPath)) {
    throw "Sparse package is enabled, but -SparsePfxPath is empty. Provide signing certificate path for installable MSIX."
}

if (-not (Test-Path $offlineRuntimeDir)) {
    New-Item -ItemType Directory -Path $offlineRuntimeDir | Out-Null
}

foreach ($runtime in $Runtimes) {
    $appArch = switch ($runtime) {
        "win-x64" { "x64" }
        "win-x86" { "x86" }
        default { throw "Unsupported runtime '$runtime'. Supported values: win-x64, win-x86" }
    }
    $platformTarget = switch ($runtime) {
        "win-x64" { "x64" }
        "win-x86" { "x86" }
        default { "AnyCPU" }
    }
    $runtimeInstallerUrl = switch ($runtime) {
        "win-x64" { "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" }
        "win-x86" { "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x86.exe" }
        default { throw "Unsupported runtime '$runtime' for runtime bootstrap download." }
    }
    $runtimeInstallerPath = Join-Path $offlineRuntimeDir "dotnet-desktop-runtime-8-$appArch.exe"

    $publishDir = Join-Path $repoRoot "bin\$Configuration\net8.0-windows\$runtime\publish"

    Write-Host "[3/6][$runtime] Build COM shell extension for $platformTarget..."
    dotnet build (Join-Path $repoRoot "ShellExtension\PriorityManagerX.ShellExtension.csproj") -c $Configuration -p:PlatformTarget=$platformTarget
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build (ShellExtension) failed with exit code $LASTEXITCODE for runtime $runtime"
    }

    Write-Host "[4/6][$runtime] Publish self-contained app..."
    $publishArgs = @(
        "publish",
        (Join-Path $repoRoot "PriorityManagerX.csproj"),
        "-c", $Configuration,
        "-r", $runtime,
        "--self-contained", ($SelfContained.ToString().ToLowerInvariant()),
        "-p:PublishSingleFile=true",
        "-p:EnableCompressionInSingleFile=true"
    )
    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE for runtime $runtime"
    }

    Write-Host "[5/6][$runtime] Prepare installer staging..."
    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stagingDir | Out-Null

    $appExe = Join-Path $publishDir "PriorityManagerX.exe"
    if (-not (Test-Path $appExe)) {
        throw "Published executable not found: $appExe"
    }

    Copy-Item (Join-Path $publishDir "*") $stagingDir -Force

    foreach ($file in $shellFiles) {
        $source = Join-Path $shellBuildDir $file
        if ($file -eq "PriorityManagerX.ShellExtension.clsidmap") {
            $source = Join-Path $shellObjDir $file
        }
        if (-not (Test-Path $source)) {
            throw "Shell extension artifact not found: $source"
        }
        Copy-Item $source (Join-Path $stagingDir $file) -Force
    }

    if ($BuildSparsePackage) {
        Write-Host "[5/6][$runtime] Build sparse/MSIX package..."
        if (-not (Test-Path $sparseBuildScript)) {
            throw "Sparse build script not found: $sparseBuildScript"
        }

        $sparseArgs = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $sparseBuildScript,
            "-Configuration", $Configuration,
            "-Runtime", $runtime,
            "-OutputMsix", "PriorityManagerX.Sparse.msix"
        )

        if ($SparsePfxPath) {
            $plainSparsePfxPassword = Convert-SecureStringToPlainText -Value $SparsePfxPassword
            $sparseArgs += @("-PfxPath", $SparsePfxPath, "-PfxPassword", $plainSparsePfxPassword)
        }

        & powershell @sparseArgs
        if ($plainSparsePfxPassword) {
            $plainSparsePfxPassword = $null
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Sparse package build failed with exit code $LASTEXITCODE for runtime $runtime"
        }

        if (-not (Test-Path $sparseMsixPath)) {
            throw "Sparse package not found after build: $sparseMsixPath"
        }

        Copy-Item $sparseMsixPath (Join-Path $stagingDir "PriorityManagerX.Sparse.msix") -Force
        if (Test-Path $sparseInstallScript) {
            Copy-Item $sparseInstallScript (Join-Path $stagingDir "install-sparse-package.ps1") -Force
        }
        if (Test-Path $sparseUninstallScript) {
            Copy-Item $sparseUninstallScript (Join-Path $stagingDir "uninstall-sparse-package.ps1") -Force
        }
    }

    if (-not (Test-Path $runtimeInstallerPath)) {
        Write-Host "[5/6][$runtime] Download offline .NET Desktop Runtime bootstrapper..."
        Invoke-WebRequest -Uri $runtimeInstallerUrl -OutFile $runtimeInstallerPath
    }

    Write-Host "[6/6][$runtime] Build installer..."
    Push-Location $scriptDir
    try {
        & $isccPath "/DAppArch=$appArch" $issFile
        if ($LASTEXITCODE -ne 0) {
            throw "ISCC failed with exit code $LASTEXITCODE for runtime $runtime"
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "Done. Installer output:" (Join-Path $scriptDir "output")
