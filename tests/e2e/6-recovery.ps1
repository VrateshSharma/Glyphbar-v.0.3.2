# The recovery paths: hiding and restoring the bar, restarting the interface, and resetting settings.
. "$PSScriptRoot\ui.ps1"
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$work0 = [GbWin]::WorkArea()
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7

function Section([string]$name) {
    $item = All (Root 'Glyphbar Settings') $CT::ListItem | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 800
}

# Hide the bar, then bring it back with the recovery command.
Show 'settings'
Section 'Taskbar'
Toggle (ByName (Root 'Glyphbar Settings') 'Show taskbar')
Start-Sleep -Seconds 1.5
Check 'Hiding the bar removes it' ($null -eq (Bar)) ([GbWin]::WorkArea())
Check 'Hiding the bar releases the work area' ([GbWin]::WorkArea() -eq $work0) ([GbWin]::WorkArea())
Check 'App keeps running with the bar hidden' ($null -ne (Gb-Pid))
Show 'taskbar'
Start-Sleep -Seconds 1.5
Check '--show:taskbar restores the bar' ($null -ne (Bar)) (BarRect)
Check 'Restored bar reserves space again' ([GbWin]::WorkArea() -ne $work0) ([GbWin]::WorkArea())

# Hide the bar while it is the only window, which is what the tray's toggle does.
Section 'About'
Press (ByName (Root 'Glyphbar Settings') 'Restart interface' $CT::Button)
Start-Sleep -Seconds 3
Check 'Only the bar is open after a restart' ((Hwnd 'Glyphbar Settings') -eq [IntPtr]::Zero)
Show 'settings'
Section 'Taskbar'
Toggle (ByName (Root 'Glyphbar Settings') 'Show taskbar')
Start-Sleep -Seconds 1
(Root 'Glyphbar Settings').GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
Start-Sleep -Seconds 2
Check 'App survives having no windows at all' ($null -ne (Gb-Pid))
Show 'taskbar'
Start-Sleep -Seconds 1.5
Check 'Bar comes back from having no windows' ($null -ne (Bar)) (BarRect)
Show 'settings'

# Restart interface: everything is rebuilt in the same process.
Section 'About'
$id = Gb-Pid
Press (ByName (Root 'Glyphbar Settings') 'Restart interface' $CT::Button)
Start-Sleep -Seconds 3
Check 'Restart interface keeps the process' ((Gb-Pid) -eq $id)
Check 'Restart interface rebuilds the bar' ($null -ne (Bar)) (BarRect)
Check 'Work area is reserved after the restart' ([GbWin]::WorkArea() -ne $work0) ([GbWin]::WorkArea())
Show 'search'
Check 'Overlays reopen after the restart' ($null -ne (Root 'Glyphbar Search'))
Show 'search'

# Reset settings, while keeping events and pins.
Show 'settings'
Section 'Taskbar'
SetRange (ByName (Root 'Glyphbar Settings') 'Height' $CT::Slider) 70
Start-Sleep -Seconds 1.2
$changed = BarRect

# Sliders that snap their value once sent the setter into unbounded recursion and crashed the app.
foreach ($value in 80, 77, 93.5, 41) {
    SetRange (ByName (Root 'Glyphbar Settings') 'Opacity (%)' $CT::Slider) $value
    Start-Sleep -Milliseconds 300
}
Check 'Opacity slider survives awkward values' ($null -ne (Gb-Pid))
Start-Sleep -Seconds 1
Check 'Opacity is saved' ([math]::Abs((Settings).Taskbar.Opacity - 0.41) -lt 0.011) "$((Settings).Taskbar.Opacity)"
Section 'About'
Press (ByName (Root 'Glyphbar Settings') 'Reset all settings' $CT::Button)
Start-Sleep -Seconds 1
Press (ByName (Root 'Glyphbar Settings') 'Reset' $CT::Button)
Start-Sleep -Seconds 2
Check 'Reset returns the bar to its default size' ((BarRect) -match '^8,1020,1912,1072$') "$changed -> $(BarRect)"
Check 'App survives a reset' ($null -ne (Gb-Pid))
Check 'Reset restores the default opacity' ([math]::Abs((Settings).Taskbar.Opacity - 0.92) -lt 0.001) "$((Settings).Taskbar.Opacity)"
Check 'Reset keeps pinned apps' ((Db "select count(*) from Applications where IsPinned=1") -notmatch '\(0,\)') (Db "select count(*) from Applications where IsPinned=1")

Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Exit is clean' (-not (Gb-Pid)) ([GbWin]::WorkArea())
Check 'Work area is restored' ([GbWin]::WorkArea() -eq $work0) ([GbWin]::WorkArea())
Gb-Log | Select-String ' Error | Critical |Unhandled' | Select-Object -Last 6 | ForEach-Object { $_.Line }
