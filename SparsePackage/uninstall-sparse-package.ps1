param(
    [string]$PackageName = "PriorityManagerX.SparsePackage",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$StoreScope = "LocalMachine"
)

$ErrorActionPreference = "Stop"

function Remove-CertByThumbprint {
    param(
        [string]$StoreLocation,
        [string]$Thumbprint
    )

    $storePath = "Cert:\$StoreLocation"
    $cert = Get-ChildItem -Path $storePath -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint } |
        Select-Object -First 1

    if ($cert) {
        Remove-Item -Path $cert.PSPath -Force -ErrorAction SilentlyContinue
    }
}

$pkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($pkg) {
    $installLocation = $pkg.InstallLocation
    $manifestPath = Join-Path $installLocation "AppxManifest.xml"
    $publisher = $null
    try {
        [xml]$manifest = Get-Content -Path $manifestPath -ErrorAction Stop
        $publisher = $manifest.Package.Identity.Publisher
    } catch {
    }

    Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue

    if ($publisher) {
        $rootStore = "$StoreScope\Root"
        $trustedStore = "$StoreScope\TrustedPeople"

        $toDelete = @()
        $toDelete += Get-ChildItem -Path ("Cert:\" + $rootStore) -ErrorAction SilentlyContinue | Where-Object { $_.Subject -eq $publisher }
        $toDelete += Get-ChildItem -Path ("Cert:\" + $trustedStore) -ErrorAction SilentlyContinue | Where-Object { $_.Subject -eq $publisher }

        foreach ($cert in $toDelete | Select-Object -Unique Thumbprint, PSPath) {
            Remove-CertByThumbprint -StoreLocation $rootStore -Thumbprint $cert.Thumbprint
            Remove-CertByThumbprint -StoreLocation $trustedStore -Thumbprint $cert.Thumbprint
        }
    }
}

Write-Host "Sparse package uninstall finished."
