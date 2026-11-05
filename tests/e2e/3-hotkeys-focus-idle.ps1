# End-to-end pass 3: blank-title validation, real hotkeys and focus, media control, idle cost.
. "$PSScriptRoot\ui.ps1"
if (-not ('GbKeys' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class GbKeys {
  [DllImport("user32")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32")] public static extern IntPtr GetForegroundWindow();
  public static void Chord(params byte[] keys){ foreach(var k in keys) keybd_event(k,0,0,UIntPtr.Zero); for(int i=keys.Length-1;i>=0;i--) keybd_event(k2(keys[i]),0,2,UIntPtr.Zero); }
  static byte k2(byte b){return b;}
}
'@
}
$VK_LWIN = 0x5B; $VK_SHIFT = 0x10; $VK_ALT = 0x12; $VK_ESC = 0x1B
$env:GLYPHBAR_POWER_DRY_RUN = '1'
$env:GLYPHBAR_KEEP_OVERLAYS = '1'

$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7
Show 'dashboard'
$d = Root 'Glyphbar'
Press (ByName $d 'New event' $CT::Button); Start-Sleep -Seconds 1
Press (ByName $d 'Save' $CT::Button); Start-Sleep -Milliseconds 800
Check 'Blank title is rejected' ((Texts $d) -contains 'Add a title.')
Press (ByName $d 'Cancel' $CT::Button); Start-Sleep -Milliseconds 800
Check 'Rejected event was not saved' ((Db "select count(*) from CalendarEvents") -match '\(0,\)')

# Widget edit mode: resize through the real control
$edit = ByName $d 'Edit layout'
Toggle $edit; Start-Sleep -Milliseconds 800
$size0 = Db "select Size from Widgets where WidgetType='Calendar'"
$resize = All $d $CT::Button | Where-Object { $_.Current.Name -eq 'Change size' }
Check 'Edit mode shows resize controls' (@($resize).Count -ge 5) "$(@($resize).Count) widgets"
Toggle $edit; Start-Sleep -Milliseconds 400

# Real hotkey: this gesture is registered by Glyphbar, so the keystroke is consumed by us.
Start-Process $exe -ArgumentList '--exit' -Wait; Start-Sleep -Seconds 3
Remove-Item Env:GLYPHBAR_KEEP_OVERLAYS
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7
$reg = Gb-Log | Select-String 'Hotkey fallback' | Select-Object -Last 3 | ForEach-Object { $_.Line.Substring(13) }
$reg
[GbKeys]::Chord($VK_LWIN, $VK_ALT, 0x20)   # Win+Alt+Space, the fallback for Search on this machine
Start-Sleep -Milliseconds 1500
$fg = [GbKeys]::GetForegroundWindow()
$search = Hwnd 'Glyphbar Search'
Check 'Hotkey opens search' ($search -ne [IntPtr]::Zero)
Check 'Search takes real keyboard focus' ($fg -eq $search) "foreground=$fg search=$search"
Start-Sleep -Milliseconds 1500
Check 'Overlay stays open while focused' ((Hwnd 'Glyphbar Search') -ne [IntPtr]::Zero)
if ($fg -eq $search) {
    [GbKeys]::Chord($VK_ESC)
    Start-Sleep -Milliseconds 800
    Check 'Escape closes search' ((Hwnd 'Glyphbar Search') -eq [IntPtr]::Zero)
}

# Idle cost, measured with nothing on screen but the bar.
Start-Sleep -Seconds 5
$proc = Get-Process -Id (Gb-Pid)
$c0 = $proc.TotalProcessorTime; $t0 = Get-Date
Start-Sleep -Seconds 30
$proc.Refresh()
$cpu = (($proc.TotalProcessorTime - $c0).TotalMilliseconds / ((Get-Date) - $t0).TotalMilliseconds) / [Environment]::ProcessorCount * 100
"idle CPU over 30s: {0:N3}% of machine  | threads {1} | private {2:N0} MB | working set {3:N0} MB" -f $cpu, $proc.Threads.Count, ($proc.PrivateMemorySize64 / 1MB), ($proc.WorkingSet64 / 1MB)
Check 'Idle CPU is negligible' (($null -ne $cpu) -and ($cpu -lt 0.5)) ("{0:N3}%" -f $cpu)

Start-Process $exe -ArgumentList '--exit' -Wait; Start-Sleep -Seconds 3
Check 'Exit is clean' (-not (Gb-Pid)) ([GbWin]::WorkArea())
