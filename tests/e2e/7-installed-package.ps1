# Verifies the installed MSIX. It runs with real power actions, so this suite never opens the power menu.
. "$PSScriptRoot\ui.ps1"
Remove-Item Env:GLYPHBAR_DATA_DIR -ErrorAction SilentlyContinue
$pkg = Get-AppxPackage Glyphbar
if (-not $pkg) { 'Glyphbar is not installed.'; return }
$aumid = "shell:AppsFolder\$($pkg.PackageFamilyName)!Glyphbar"
$data = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalCache\Local\Glyphbar"
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\Glyphbar.exe'
function Send([string]$arg) { Start-Process $alias -ArgumentList $arg; Start-Sleep -Seconds 2 }
function PkgLog { Get-ChildItem "$data\logs" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1 | ForEach-Object { Get-Content $_.FullName -Tail 60 } }

$work0 = [GbWin]::WorkArea()
$t0 = Get-Date
Check 'Package status is OK' ($pkg.Status -eq 'Ok') "$($pkg.PackageFullName)"
Start-Process $aumid
Start-Sleep -Seconds 9
$id = Gb-Pid
$gp = if ($id) { Get-Process -Id $id }
Check 'Starts from the Start menu entry' ($null -ne $gp)
if (-not $gp) { return }
Check 'Runs from the package' ($gp.Path -match 'WindowsApps') "$($gp.Path)"
Check 'Self-contained: uses its own runtime' ((Test-Path (Join-Path $pkg.InstallLocation 'coreclr.dll')))
# An auto-hide bar reserves nothing, so this only applies with that off. The suite uses whatever the user has set.
$userSettings = Get-Content "$data\settings.json" -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
if (-not $userSettings.Taskbar.AutoHide) { Check 'Appbar registers' ([GbWin]::WorkArea() -ne $work0) ([GbWin]::WorkArea()) }
Check 'Taskbar XAML loads' ($null -ne (Bar)) (BarRect)
Check 'Data goes to the package folder' (Test-Path "$data\glyphbar.db") $data
Check 'Execution alias is registered' (Test-Path $alias) $alias
Gb-Shot 'pkg-bar' | Out-Null

# Do arguments reach a packaged app?
Send '--show:settings'
Start-Sleep -Seconds 2
$s = Root 'Glyphbar Settings'
$argsWork = $null -ne $s
Check 'Arguments reach the running packaged app' $argsWork
"ARGS_WORK=$argsWork"
if ($s) {
    $about = All $s $CT::ListItem | Where-Object { $_.Current.Name -eq 'About' } | Select-Object -First 1
    $about.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 800
    $t = Texts (Root 'Glyphbar Settings')
    Check 'About shows the packaged build' (($t -match 'Packaged \(MSIX\)').Count -gt 0) (($t -match 'MSIX|Unpackaged') -join ' ')

    # Startup task: the path only an installed package can exercise.
    $sys = All (Root 'Glyphbar Settings') $CT::ListItem | Where-Object { $_.Current.Name -eq 'System' } | Select-Object -First 1
    $sys.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 800
    $key = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\$($pkg.PackageFamilyName)\GlyphbarStartup"
    $sw = ByName (Root 'Glyphbar Settings') 'Launch at Windows sign-in'
    $st0 = (Get-ItemProperty $key -ErrorAction SilentlyContinue).State
    Toggle $sw; Start-Sleep -Seconds 2.5
    $st1 = (Get-ItemProperty $key -ErrorAction SilentlyContinue).State
    Check 'The switch changes the startup task' ($st1 -ne $st0) "State $st0 -> $st1"
    Check 'Packaged build does not use the Run key' ($null -eq (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Glyphbar)
    Toggle (ByName (Root 'Glyphbar Settings') 'Launch at Windows sign-in'); Start-Sleep -Seconds 2.5
    $st2 = (Get-ItemProperty $key -ErrorAction SilentlyContinue).State
    Check 'Switching back restores the user''s setting' ($st2 -eq $st0) "State $st1 -> $st2"
}

"--- log problems"
PkgLog | Select-String ' Error | Critical |Unhandled|Could not' | Select-Object -Last 6 | ForEach-Object { $_.Line }

# Exit. Being alive first matters: a crash would otherwise look like a successful exit.
Check 'Still alive before exit' ((Gb-Pid) -eq $id)
if ($argsWork) { Send '--exit'; Start-Sleep -Seconds 4 }
$stillRunning = [bool](Gb-Pid)
Check 'Packaged app exits through --exit' (-not $stillRunning)
"STILL_RUNNING=$stillRunning"
Check 'Work area restored' ([GbWin]::WorkArea() -eq $work0) ([GbWin]::WorkArea())
$crashes = @(Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$t0; Id=1000} -ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'Glyphbar' })
Check 'No crashes' ($crashes.Count -eq 0) "$($crashes.Count)"
$crashes | ForEach-Object { [regex]::Match($_.Message,'Exception code: (\S+)').Value }
