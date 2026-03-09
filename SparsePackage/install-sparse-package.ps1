param(
    [string]$MsixPath = "",
    [string]$PackageName = "PriorityManagerX.SparsePackage",
    [string]$StageDir = "C:\Temp",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$StoreScope = "LocalMachine"
)

$ErrorActionPreference = "Stop"

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-CertInStore {
    param(
        [string]$CertPath,
        [string]$StoreLocation,
        [string]$Thumbprint
    )

    $storePath = "Cert:\$StoreLocation"
    $existing = Get-ChildItem -Path $storePath -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $Thumbprint }
    if (-not $existing) {
        Import-Certificate -FilePath $CertPath -CertStoreLocation $storePath | Out-Null
        Write-Host "Imported certificate to $storePath"
    } else {
        Write-Host "Certificate already present in $storePath"
    }
}

$root = Split-Path -Parent $PSScriptRoot

if (-not $MsixPath) {
    $MsixPath = Join-Path $root "PriorityManagerX.Sparse.msix"
}

if (-not (Test-Path $MsixPath)) {
    throw "MSIX not found: $MsixPath"
}

if ($StoreScope -eq "LocalMachine" -and -not (Test-IsAdmin)) {
    throw "Run this script as Administrator for StoreScope=LocalMachine."
}

New-Item -ItemType Directory -Path $StageDir -Force | Out-Null
$stagedMsix = Join-Path $StageDir ([IO.Path]::GetFileName($MsixPath))
Copy-Item -Path $MsixPath -Destination $stagedMsix -Force
Write-Host "Staged MSIX: $stagedMsix"

$sig = Get-AuthenticodeSignature -FilePath $stagedMsix
if (-not $sig.SignerCertificate) {
    throw "Signer certificate not found in MSIX signature."
}

$thumbprint = $sig.SignerCertificate.Thumbprint
$certPath = Join-Path $StageDir "pmx-signer.cer"
Export-Certificate -Cert $sig.SignerCertificate -FilePath $certPath -Force | Out-Null
Write-Host "Signer cert: $($sig.SignerCertificate.Subject) ($thumbprint)"

$rootStore = "$StoreScope\Root"
$trustedPeopleStore = "$StoreScope\TrustedPeople"

Ensure-CertInStore -CertPath $certPath -StoreLocation $rootStore -Thumbprint $thumbprint
Ensure-CertInStore -CertPath $certPath -StoreLocation $trustedPeopleStore -Thumbprint $thumbprint

Add-AppxPackage -Path $stagedMsix -ForceApplicationShutdown

$installed = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if (-not $installed) {
    throw "Installation command finished, but package '$PackageName' was not found."
}

Write-Host "Installed package: $($installed.PackageFullName)"
