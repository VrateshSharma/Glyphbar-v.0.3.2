<#
.SYNOPSIS
    Builds and signs artifacts\Glyphbar.msix using only the .NET SDK. makeappx and signtool come from NuGet.

.NOTES
    Creates a self-signed certificate in the current user's store and exports it to artifacts\, which git ignores.
    Nothing here changes which certificates the machine trusts. tools\install-msix.ps1 does that, after asking.
#>
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'artifacts'
$publisher = 'CN=Glyphbar Local'
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item (Join-Path $out 'sdk-package') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'Building package...'
dotnet build (Join-Path $root 'src\Glyphbar.App\Glyphbar.App.csproj') -c $Configuration -r win-x64 -p:Platform=x64 `
    -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -v q -nologo
if ($LASTEXITCODE) { throw 'Package build failed' }

$built = Get-ChildItem (Join-Path $out 'sdk-package') -Recurse -Filter *.msix | Select-Object -First 1
if (-not $built) { throw 'The build produced no .msix' }
$msix = Join-Path $out 'Glyphbar.msix'
Copy-Item $built.FullName $msix -Force

$tools = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter signtool.exe |
    Where-Object FullName -match '\\x64\\' | Sort-Object FullName | Select-Object -Last 1 | ForEach-Object DirectoryName

$pfx = Join-Path $out 'Glyphbar-local.pfx'
$cer = Join-Path $out 'Glyphbar-local.cer'
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $publisher -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature -FriendlyName 'Glyphbar local signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') -NotAfter (Get-Date).AddYears(3)
}
# A throwaway password for a local, self-signed file that never leaves artifacts\.
$password = ConvertTo-SecureString 'glyphbar-local' -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $password | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
& "$tools\signtool.exe" sign /fd SHA256 /f $pfx /p 'glyphbar-local' $msix | Out-Null
if ($LASTEXITCODE) { throw 'signtool failed' }

Write-Host ("Built {0} ({1} MB), signed by {2}" -f $msix, [math]::Round((Get-Item $msix).Length / 1MB, 1), $publisher)
