<#
.SYNOPSIS
    Removes Glyphbar and the local certificate that install-msix.ps1 trusted. Add -KeepData to keep settings and events.
#>
param([switch]$KeepData)
$ErrorActionPreference = 'Stop'

$pkg = Get-AppxPackage Glyphbar
if ($pkg) {
    # Ask the app to leave first, so it hands its reserved screen space back.
    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\Glyphbar.exe'
    if (Get-Process Glyphbar -ErrorAction SilentlyContinue) {
        Start-Process $alias -ArgumentList '--exit' -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    if ($KeepData) { Remove-AppxPackage $pkg.PackageFullName -PreserveApplicationData }
    else { Remove-AppxPackage $pkg.PackageFullName }
    Write-Host 'Glyphbar removed.'
}
else {
    Write-Host 'Glyphbar is not installed.'
}

$command = "Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=Glyphbar Local' | Remove-Item"
if (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=Glyphbar Local') {
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $command
    Write-Host 'Local certificate removed.'
}
