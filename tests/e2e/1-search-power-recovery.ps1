# End-to-end pass 1: search, launching, power flow, recovery. The power executor is the dry-run stand-in.
. "$PSScriptRoot\ui.ps1"
$env:GLYPHBAR_POWER_DRY_RUN = '1'
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$work0 = [GbWin]::WorkArea()
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7
Check 'App starts and stays running' (-not $p.HasExited)
Check 'Appbar reserves work area' ([GbWin]::WorkArea() -ne $work0) ([GbWin]::WorkArea())

# Search: applications
Show 'search'
$r = Root 'Glyphbar Search'
Check 'Search overlay opens' ($null -ne $r)
$box = (All $r $CT::Edit)[0]
SetText $box 'calcul'
Start-Sleep -Milliseconds 1500
$t = Texts $r
Check 'App search finds Calculator' ($t -contains 'Calculator') (($t | Select-Object -First 8) -join ' | ')

# Search: settings
SetText $box 'wallpaper'
Start-Sleep -Milliseconds 1200
$t = Texts $r
Check 'Settings search by keyword' ($t -contains 'Personalisation') (($t | Select-Object -First 6) -join ' | ')

# Search: real indexed file. A file this test creates in an indexed-by-default location would need indexing time, so use a common name.
SetText $box 'readme'
Start-Sleep -Milliseconds 3000
$t = Texts $r
Check 'File search returns indexed results' (($t -contains 'FILES') -or ($t -contains 'FOLDERS')) (($t | Select-Object -First 8) -join ' | ')
Check 'Windows Search provider available' (($t.Count -gt 0) -and -not ($t -match 'unavailable'))

# No results message
SetText $box 'zzqqxxnothing'
Start-Sleep -Milliseconds 2000
$t = Texts $r
Check 'Empty state shown' (($t -match 'No results') -as [bool]) (($t | Select-Object -First 4) -join ' | ')

# Launch from search
SetText $box 'calculator'
Start-Sleep -Milliseconds 1200
$before = @(Get-Process CalculatorApp -ErrorAction SilentlyContinue).Count
$item = (All $r $CT::ListItem)[0]
try { Press $item } catch { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
Start-Sleep -Seconds 3
$after = @(Get-Process CalculatorApp -ErrorAction SilentlyContinue).Count
Check 'Launching an app from search works' ($after -gt $before) "CalculatorApp before=$before after=$after"
Start-Sleep -Seconds 1
$dock = All (Root 'Glyphbar Taskbar') $CT::ListItem | ForEach-Object { $_.Current.Name }
Check 'Launch recorded in history' ((Db "select LaunchCount from Applications where AppId like '%Calculator%'") -match '\d') (Db "select AppId,LaunchCount from Applications where LaunchCount>0")
Get-Process CalculatorApp -ErrorAction SilentlyContinue | ForEach-Object { [void]$_.CloseMainWindow() }
Start-Sleep -Seconds 1
Get-Process CalculatorApp -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if ((Hwnd 'Glyphbar Search') -ne [IntPtr]::Zero) { Show 'search' }

# Power: all flows run against the dry-run executor.
Show 'power'
$r = Root 'Glyphbar Power'
Check 'Power menu opens' ($null -ne $r)
$names = All $r $CT::Button | ForEach-Object { $_.Current.Name }
$t = Texts $r
foreach ($n in 'Lock', 'Sleep', 'Sign out', 'Restart', 'Shut down') { Check "Power action present: $n" ($t -contains $n) }

$btn = All $r $CT::Button | Where-Object { ($_ | ForEach-Object { Texts $_ }) -contains 'Shut down' } | Select-Object -First 1
Press $btn
Start-Sleep -Milliseconds 700
$t = Texts $r
Check 'Shutdown asks for confirmation' ($t -contains 'Shut down this PC?') ($t -join ' | ')
Check 'Nothing executed before confirming' (-not ((Gb-Log) -match 'DRY RUN'))
Press (ByName $r 'Cancel')
Start-Sleep -Milliseconds 500
Check 'Cancel returns to the menu' ((Texts $r) -contains 'Restart')
Check 'Cancel executed nothing' (-not ((Gb-Log) -match 'DRY RUN'))

$btn = All $r $CT::Button | Where-Object { ($_ | ForEach-Object { Texts $_ }) -contains 'Restart' } | Select-Object -First 1
Press $btn
Start-Sleep -Milliseconds 700
Press (ByName $r 'Restart' $CT::Button)
Start-Sleep -Seconds 1
Check 'Confirmed restart reaches the executor' (((Gb-Log) -match 'DRY RUN: would execute Restart') -as [bool])
Check 'Power menu closes after an action' ((Hwnd 'Glyphbar Power') -eq [IntPtr]::Zero)

# Recovery: simulate Explorer's TaskbarCreated broadcast instead of killing Explorer.
$rect = BarRect
$n = [GbWin]::BroadcastTo((Gb-Pid), 'TaskbarCreated')
Start-Sleep -Seconds 2
Check 'TaskbarCreated handled' (((Gb-Log) -match 'Explorer restarted') -as [bool]) "sent to $n windows"
Check 'Bar in same place after shell restart' ((BarRect) -eq $rect) (BarRect)
Check 'Work area still reserved after shell restart' ([GbWin]::WorkArea() -ne $work0) ([GbWin]::WorkArea())

# Single instance: a second normal launch must not create a second process.
Start-Process $exe -Wait
Start-Sleep -Seconds 1
$live = @(Get-Process Glyphbar | Where-Object { $_.Threads.Count -gt 0 }).Count
Check 'Second launch does not start a second process' ($live -eq 1) "processes=$live"

Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Exit ends the process' (-not (Gb-Pid))
Check 'Exit restores the work area' ([GbWin]::WorkArea() -eq $work0) ([GbWin]::WorkArea())
'--- log problems'
Gb-Log | Select-String 'Error|Critical|Exception|Unhandled' | Select-Object -Last 8 | ForEach-Object { $_.Line }
