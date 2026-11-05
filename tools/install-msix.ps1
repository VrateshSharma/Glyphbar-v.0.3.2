<#
.SYNOPSIS
    Installs artifacts\Glyphbar.msix. Windows only installs packages from trusted signers, so this adds the local
    self-signed certificate to the machine's Trusted People store, which needs one administrator prompt.
    tools\uninstall-msix.ps1 reverses it.
#>
$ErrorActionPreference = 'Stop'
$out = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'artifacts'
$msix = Join-Path $out 'Glyphbar.msix'
$cer = Join-Path $out 'Glyphbar-local.cer'
if (-not (Test-Path $msix)) { throw 'artifacts\Glyphbar.msix not found. Run tools\build-msix.ps1 first.' }

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host 'Asking Windows for permission to trust the local certificate...'
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', "Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
}
else {
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

Add-AppxPackage -Path $msix -ForceApplicationShutdown
Write-Host 'Installed. Find Glyphbar in the Start menu.'
