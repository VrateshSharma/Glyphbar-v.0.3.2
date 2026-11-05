# UI Automation helpers: drive Glyphbar without sending keystrokes anywhere.
. "$PSScriptRoot\gb.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$script:AE = [System.Windows.Automation.AutomationElement]
$script:TS = [System.Windows.Automation.TreeScope]
$script:CT = [System.Windows.Automation.ControlType]
$script:Results = New-Object System.Collections.ArrayList

function Check([string]$what, [bool]$ok, [string]$detail = '') {
    $line = '{0}  {1}{2}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $what, $(if ($detail) { "  ($detail)" } else { '' })
    [void]$script:Results.Add($line); $line
}
function Hwnd([string]$titleRegex) {
    $id = Gb-Pid
    foreach ($l in [GbWin]::Windows($id)) { if ($l -match "'$titleRegex'$" -and $l -match 'vis=True') { return [IntPtr][long]($l -split ' ')[0] } }
    [IntPtr]::Zero
}
function Root([string]$titleRegex) {
    $h = Hwnd $titleRegex
    if ($h -eq [IntPtr]::Zero) { return $null }
    $script:AE::FromHandle($h)
}
function All($root, $type) {
    $c = New-Object System.Windows.Automation.PropertyCondition($script:AE::ControlTypeProperty, $type)
    $root.FindAll($script:TS::Descendants, $c)
}
function ByName($root, [string]$name, $type = $null) {
    $c = New-Object System.Windows.Automation.PropertyCondition($script:AE::NameProperty, $name)
    foreach ($e in $root.FindAll($script:TS::Descendants, $c)) {
        if ($null -eq $type -or $e.Current.ControlType -eq $type) { return $e }
    }
    $null
}
function Texts($root) { (All $root $script:CT::Text | ForEach-Object { $_.Current.Name }) }
function Press($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Toggle($el) { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
function SetText($el, [string]$v) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function SetRange($el, [double]$v) { $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($v) }
function Show([string]$what) {
    # Without a running instance this would start a new one and wait on it forever.
    if (-not (Gb-Pid)) { throw "Glyphbar is not running, so --show:$what cannot be sent" }
    Start-Process $script:exe -ArgumentList "--show:$what" -Wait; Start-Sleep -Milliseconds 900
}
function Bar { [GbWin]::Windows((Gb-Pid)) | Where-Object { $_ -match 'Taskbar' } | Select-Object -First 1 }
function BarRect { if ((Bar) -match '\[(.+?)\]') { $Matches[1] } }
function Settings { Get-Content "$script:sp\gbdata\settings.json" -Raw | ConvertFrom-Json }
function Db([string]$sql) { python -I -c "import sqlite3,sys;c=sqlite3.connect(sys.argv[1]);print(list(c.execute(sys.argv[2])))" "$script:sp\gbdata\glyphbar.db" $sql }
