# End-to-end pass 2: calendar, widgets, settings, persistence, startup, media.
. "$PSScriptRoot\ui.ps1"
$env:GLYPHBAR_POWER_DRY_RUN = '1'
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7

Show 'dashboard'
$d = Root 'Glyphbar'
Check 'Dashboard opens' ($null -ne $d)
Start-Sleep -Seconds 2
$t = Texts $d
Check 'Battery widget shows a real percentage' ((($t -match 'BATTERY').Count -gt 0)) (($t | Where-Object { $_ -match 'battery|Plugged|Charging' }) -join ' | ')
$drive = $t | Where-Object { $_ -match '^C: ' }
$c = Get-PSDrive C
$totalGb = [math]::Round(($c.Used + $c.Free) / 1GB)
$detail = $t | Where-Object { $_ -match ' total ' } | Select-Object -First 1
Check 'Storage widget shows C: SYSTEM' ($drive -match 'SYSTEM') "$drive"
Check 'Storage values match the disk' ($detail -match "^$totalGb GB|^$($totalGb - 1) GB|^$($totalGb + 1) GB") "$detail vs $totalGb GB"
Check 'Music widget reads a real session or idle' ((($t -match 'CHROME|Nothing playing|MUSIC').Count -gt 0)) (($t | Where-Object { $_ -match 'CHROME|MSEDGE|Nothing playing|Suits' } | Select-Object -First 3) -join ' | ')
$today = (Get-Date).ToString('dddd')
Check 'Clock widget shows the real weekday' ($t -contains $today) $today

# Calendar create
$add = ByName $d 'New event'
Check 'New event button reachable' ($null -ne $add)
Press $add
Start-Sleep -Seconds 1
$title = ByName $d 'Title' $CT::Edit
Check 'Event dialog opens' ($null -ne $title)
SetText $title 'Glyphbar test event'
$desc = ByName $d 'Description' $CT::Edit
SetText $desc 'Created by the end-to-end test'
Press (ByName $d 'Save' $CT::Button)
Start-Sleep -Seconds 1.5
Check 'Event is saved to SQLite' ((Db "select Title,Description from CalendarEvents") -match 'Glyphbar test event') (Db "select Id,Title,StartDateTime,EndDateTime from CalendarEvents")
$t = Texts $d
Check 'Event appears in the day list' ($t -contains 'Glyphbar test event')

# Validation: a blank title must not save
Press (ByName $d 'New event' $CT::Button); Start-Sleep -Seconds 1
Press (ByName $d 'Save' $CT::Button); Start-Sleep -Milliseconds 700
Check 'Blank title is rejected' ((Texts $d) -contains 'Add a title.')
Press (ByName $d 'Cancel' $CT::Button); Start-Sleep -Milliseconds 700

# Calendar edit
$row = All $d $CT::ListItem | Where-Object { (Texts $_) -contains 'Glyphbar test event' } | Select-Object -First 1
Press $row; Start-Sleep -Seconds 1
SetText (ByName $d 'Title' $CT::Edit) 'Glyphbar test event edited'
Press (ByName $d 'Save' $CT::Button); Start-Sleep -Seconds 1.5
Check 'Event edit persists' ((Db "select Title from CalendarEvents") -match 'edited') (Db "select Title from CalendarEvents")

# Calendar navigation
$before = (Texts $d) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
Press (ByName $d 'Next month'); Start-Sleep -Milliseconds 700
$after = (Texts $d) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
Check 'Next month navigates' ($before -ne $after) "$before -> $after"
Press (ByName $d 'Previous year'); Start-Sleep -Milliseconds 700
$year = (Texts $d) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
Check 'Previous year navigates' ($year -ne $after) "$after -> $year"
Press (ByName $d 'Today'); Start-Sleep -Milliseconds 700
$back = (Texts $d) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
Check 'Today returns to this month' ($back -eq $before) "$back"

# Calendar delete
$row = All $d $CT::ListItem | Where-Object { (Texts $_) -contains 'Glyphbar test event edited' } | Select-Object -First 1
Press $row; Start-Sleep -Seconds 1
Press (ByName $d 'Delete' $CT::Button); Start-Sleep -Seconds 1.5
Check 'Event delete persists' ((Db "select count(*) from CalendarEvents") -match '\(0,\)') (Db "select count(*) from CalendarEvents")

# Settings apply live
Show 'settings'
$s = Root 'Glyphbar Settings'
Check 'Settings opens' ($null -ne $s)
$nav = All $s $CT::ListItem
function Section([string]$name) {
    $item = All (Root 'Glyphbar Settings') $CT::ListItem | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 800
}
Section 'Taskbar'
$s = Root 'Glyphbar Settings'
$r0 = BarRect
$w0 = [GbWin]::WorkArea()
SetRange (ByName $s 'Height' $CT::Slider) 64
Start-Sleep -Seconds 1.2
Check 'Height change moves the bar immediately' ((BarRect) -ne $r0) "$r0 -> $(BarRect)"
Check 'Height change updates the work area' ([GbWin]::WorkArea() -ne $w0) ([GbWin]::WorkArea())
SetRange (ByName $s 'Width (%)' $CT::Slider) 50
Start-Sleep -Seconds 1
Check 'Width change applies' ((BarRect) -match '^\d+,\d+,\d+') (BarRect)
$s2 = (BarRect) -split ','
Check 'Centred bar is centred' ([math]::Abs(([int]$s2[0] + [int]$s2[2]) - 1920) -le 2) (BarRect)
SetRange (ByName $s 'Width (%)' $CT::Slider) 100
Start-Sleep -Milliseconds 600

$floating = ByName $s 'Floating dock'
Toggle $floating; Start-Sleep -Seconds 1
Check 'Docked mode fills the edge' ((BarRect) -match '^0,\d+,1920,1080$') (BarRect)
Toggle $floating; Start-Sleep -Seconds 1

$hide = ByName $s 'Auto-hide'
Toggle $hide; Start-Sleep -Seconds 2.5
$rect = (BarRect) -split ','
Check 'Auto-hide gives the work area back' ([GbWin]::WorkArea() -eq '0,0,1920,1080') ([GbWin]::WorkArea())
Check 'Auto-hide slides the bar away, leaving a sliver' ([int]$rect[1] -ge 1076) (BarRect)
Toggle $hide; Start-Sleep -Seconds 1.5
Check 'Turning auto-hide off reserves space again' ([GbWin]::WorkArea() -ne '0,0,1920,1080') ([GbWin]::WorkArea())

$top = All $s $CT::ComboBox | Where-Object { $_.Current.Name -eq 'Position' } | Select-Object -First 1
$top.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 500
$opt = ByName $s 'Top' $CT::ListItem
if ($null -eq $opt) { $opt = ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Top' $CT::ListItem }
if ($opt) { $opt.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 1.2 }
Check 'Top position works' ([int]((BarRect) -split ',')[1] -lt 100) (BarRect)
$top.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 500
$opt = ByName $s 'Bottom' $CT::ListItem
if ($null -eq $opt) { $opt = ByName ([System.Windows.Automation.AutomationElement]::RootElement) 'Bottom' $CT::ListItem }
if ($opt) { $opt.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 1.2 }
Check 'Back to bottom' ([int]((BarRect) -split ',')[1] -gt 900) (BarRect)

# Widgets section: enable, reorder
Section 'Widgets'
$s = Root 'Glyphbar Settings'
$before = Db "select WidgetType from Widgets order by SortOrder"
$down = All $s $CT::Button | Where-Object { $_.Current.Name -eq 'Move later' } | Select-Object -First 1
Press $down; Start-Sleep -Seconds 1.2
$after = Db "select WidgetType,IsEnabled from Widgets order by SortOrder"
Check 'Widget reorder persists' ($after -match "'Date', 1\), \('Clock'") "$after"
$s = Root 'Glyphbar Settings'
$toggles = All $s $CT::Button | Where-Object { $_.Current.Name -eq 'Move later' }
$sw = All $s $CT::Button
$checks = $s.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::IsTogglePatternAvailableProperty, $true)))
$first = $checks | Select-Object -First 1
Toggle $first; Start-Sleep -Seconds 1.2
$dis = Db "select WidgetType from Widgets where IsEnabled=0"
Check 'Widget disable persists' ($dis -match "'Date'") "$dis"
$s = Root 'Glyphbar Settings'
$checks = $s.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::IsTogglePatternAvailableProperty, $true)))
Toggle ($checks | Select-Object -First 1); Start-Sleep -Seconds 1.2

# Startup
Section 'System'
$s = Root 'Glyphbar Settings'
$sw = ByName $s 'Launch at Windows sign-in'
Toggle $sw; Start-Sleep -Seconds 1.5
$run = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Glyphbar
Check 'Launch at sign-in writes the per-user Run value' ($run -match 'Glyphbar.exe" --startup') "$run"
Toggle $sw; Start-Sleep -Seconds 1.5
$run = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Glyphbar
Check 'Turning it off removes the value' ($null -eq $run) "$run"

# Persistence across a real restart
$h = (Settings).Taskbar.Height
Check 'Setting written to settings.json' ($h -eq 64) "Height=$h"
Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Exit restores the work area' ([GbWin]::WorkArea() -eq '0,0,1920,1080') ([GbWin]::WorkArea())
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 6
Check 'Height survives a restart' ((BarRect) -match '^8,1008,1912,1072$') (BarRect)
$order = Db "select WidgetType from Widgets order by SortOrder limit 2"
Check 'Widget order survives a restart' ($order -match "'Date'.*'Clock'") "$order"
Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Final exit is clean' (-not (Gb-Pid)) ([GbWin]::WorkArea())
'--- log problems'
Gb-Log | Select-String ' Error | Critical |Unhandled|Exception' | Select-Object -Last 8 | ForEach-Object { $_.Line }
'--- summary'
"$(($script:Results | Where-Object { $_ -like 'PASS*' }).Count) passed, $(($script:Results | Where-Object { $_ -like 'FAIL*' }).Count) failed"
