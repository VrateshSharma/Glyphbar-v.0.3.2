# Pointer-driven checks that UI Automation can't do: the context menu, auto-hide after using it, and dragging the dock.
# Moves the pointer for a few seconds. Every coordinate comes from a live element inside the bar.
. "$PSScriptRoot\ui.ps1"
if (-not ('GbMouse' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Threading;
public static class GbMouse {
  [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
  [DllImport("user32")] public static extern bool GetCursorPos(out P p);
  [DllImport("user32")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32")] public static extern void mouse_event(uint f, int x, int y, int d, UIntPtr e);
  [DllImport("user32")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  const uint LD=2, LU=4, RD=8, RU=16;
  public static void RightClick(int x,int y){ SetCursorPos(x,y); Thread.Sleep(120); mouse_event(RD,0,0,0,UIntPtr.Zero); mouse_event(RU,0,0,0,UIntPtr.Zero); }
  public static void Drag(int x1,int y1,int x2,int y2){ SetCursorPos(x1,y1); Thread.Sleep(200); mouse_event(LD,0,0,0,UIntPtr.Zero); Thread.Sleep(350);
    for(int i=1;i<=24;i++){ SetCursorPos(x1+(x2-x1)*i/24,y1+(y2-y1)*i/24); Thread.Sleep(25);} Thread.Sleep(350); mouse_event(LU,0,0,0,UIntPtr.Zero); }
  public static void Esc(){ keybd_event(0x1B,0,0,UIntPtr.Zero); keybd_event(0x1B,0,2,UIntPtr.Zero); }
}
'@
}
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$start = New-Object GbMouse+P; [void][GbMouse]::GetCursorPos([ref]$start)
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 8

# Reproduce the reported sequence: unpin through the menu, then re-pin via the database-backed launcher path.
$items = All (Root 'Glyphbar Taskbar') $CT::ListItem
$first = $items | Select-Object -First 1
$r = $first.Current.BoundingRectangle
"first dock item: $($first.Current.Name)"
[GbMouse]::RightClick([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
Start-Sleep -Seconds 1.2
$desk = $AE::RootElement
$menu = $desk.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::MenuItem))) | Where-Object { $_.Current.ProcessId -eq (Gb-Pid) }
$names = @($menu | ForEach-Object { $_.Current.Name })
Check 'Right-click opens the context menu' ($names.Count -ge 2) ($names -join ' | ')
Check 'Menu offers Open and Unpin' (($names -contains 'Open') -and ($names -contains 'Unpin'))
$barTop = [int]((BarRect) -split ',')[1]
Check 'Menu is not clipped by the bar window' (@($menu)[0].Current.BoundingRectangle.Y -lt $barTop) "menu y=$(@($menu)[0].Current.BoundingRectangle.Y) bar top=$barTop"
$unpin = $desk.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'Unpin'))) | Where-Object { $_.Current.ProcessId -eq (Gb-Pid) } | Select-Object -First 1
Press $unpin; Start-Sleep -Seconds 1.2

Show 'settings'
$sw = Root 'Glyphbar Settings'
$item = All $sw $CT::ListItem | Where-Object { $_.Current.Name -eq 'Taskbar' } | Select-Object -First 1
$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 800
$sw = Root 'Glyphbar Settings'
[void][GbMouse]::SetCursorPos(960, 500)
Toggle (ByName $sw 'Auto-hide'); Start-Sleep -Seconds 2.2
$hidden = BarRect
Check 'Bar parks after a menu removed its own anchor' ([int](($hidden -split ',')[1]) -ge 1076) $hidden
[void][GbMouse]::SetCursorPos(960, 1079); Start-Sleep -Milliseconds 120; [void][GbMouse]::SetCursorPos(962, 1079)
Start-Sleep -Seconds 1
$shown = BarRect
Check 'Pointer at the edge reveals the bar' ([int](($shown -split ',')[1]) -lt 1070) $shown
[void][GbMouse]::SetCursorPos(960, 500)
Start-Sleep -Seconds 1.8
Check 'Moving away hides it again' ([int](((BarRect) -split ',')[1]) -ge 1076) (BarRect)
Toggle (ByName (Root 'Glyphbar Settings') 'Auto-hide'); Start-Sleep -Seconds 1.2

# Pin it back so two pins exist, then drag.
Show 'launcher'
$l = Root 'Glyphbar Launcher'
$tile = All $l $CT::ListItem | Where-Object { $_.Current.Name -eq 'Calculator' } | Select-Object -First 1
$tr = $tile.Current.BoundingRectangle
[GbMouse]::RightClick([int]($tr.X + $tr.Width / 2), [int]($tr.Y + $tr.Height / 2))
Start-Sleep -Seconds 1.2
$pin = $desk.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'Pin'))) | Where-Object { $_.Current.ProcessId -eq (Gb-Pid) } | Select-Object -First 1
if ($pin) { Press $pin; Start-Sleep -Seconds 1.2 }
Show 'launcher'
Start-Sleep -Milliseconds 800

$before = Db "select Name from Applications where IsPinned=1 order by SortOrder"
$items = All (Root 'Glyphbar Taskbar') $CT::ListItem
"dock now: $(($items | ForEach-Object { $_.Current.Name }) -join ' | ')"
$f = ($items | Select-Object -First 1).Current.BoundingRectangle
$s = ($items | Select-Object -Skip 1 -First 1).Current.BoundingRectangle
"drag from $([int]$f.X),$([int]$f.Y) to $([int]($s.X + $s.Width - 4))"
[GbMouse]::Drag([int]($f.X + $f.Width / 2), [int]($f.Y + $f.Height / 2), [int]($s.X + $s.Width - 4), [int]($s.Y + $s.Height / 2))
Start-Sleep -Seconds 1.5
$after = Db "select Name from Applications where IsPinned=1 order by SortOrder"
Check 'Dragging reorders pinned apps' ($after -ne $before) "$before -> $after"

[void][GbMouse]::SetCursorPos($start.X, $start.Y)
Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Exit is clean' (-not (Gb-Pid)) ([GbWin]::WorkArea())
