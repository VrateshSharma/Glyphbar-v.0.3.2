# Desktop widgets: transparent, behind apps, above the wallpaper, and still there after losing focus or Show Desktop.
# Desktop widgets: individual translucent cards.
# Toggles Show Desktop twice, so open windows briefly hide and then return.
. "$PSScriptRoot\ui.ps1"
if (-not ('GbZOrder' -as [type])) {
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class GbZOrder {
  delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32")] static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32")] static extern bool IsIconic(IntPtr h);
  [DllImport("user32",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string c,string t);
  [DllImport("user32",EntryPoint="GetWindowLongPtrW")] static extern IntPtr L(IntPtr h,int i);
  [DllImport("dwmapi")] static extern int DwmGetWindowAttribute(IntPtr h,int a,out int v,int s);
  // Returns z-positions, top first: the widget window, the desktop window, and the LOWEST window with a title bar.
  public static int[] Positions(IntPtr widget){ int i=0,w=-1,d=-1,a=-1;
    EnumWindows((h,l)=>{ var s=new StringBuilder(64); GetClassName(h,s,64); string c=s.ToString();
      if(h==widget) w=i;
      else if((c=="Progman"||c=="WorkerW") && FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero) d=i;
      else { int cl; DwmGetWindowAttribute(h,14,out cl,4); if(cl==0 && h!=widget && IsWindowVisible(h) && !IsIconic(h) && ((long)L(h,-16)&0xC00000)==0xC00000 && ((long)L(h,-20)&0x88)==0) a=i; }
      i++; return true; },IntPtr.Zero); return new[]{w,d,a}; }
}
'@
}
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$t0 = Get-Date
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 9
$h = Hwnd 'Glyphbar Desktop Widgets'
Check 'Desktop widgets appear by default' ($h -ne [IntPtr]::Zero)
$line = [GbWin]::Windows((Gb-Pid)) | Where-Object { $_ -match 'Desktop Widgets' } | Select-Object -First 1
"window: $line"
$z = [GbZOrder]::Positions($h)
"z: widget=$($z[0]) desktop=$($z[1]) topApp=$($z[2])"
Check 'Above the desktop' ($z[0] -lt $z[1]) "widget $($z[0]) desktop $($z[1])"
Check 'Below apps' ($z[0] -gt $z[2]) "widget $($z[0]) app $($z[2])"
Check 'Not in the Alt-Tab list' (-not (([GbWin]::Windows((Gb-Pid)) | Where-Object { $_ -match 'Desktop Widgets' }) -match 'Taskbar'))

$r = Root 'Glyphbar Desktop Widgets'
$before = (Texts $r) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
$next = ByName $r 'Next month' $CT::Button
if ($next) {
    Press $next; Start-Sleep -Seconds 1
    $after = (Texts $r) | Where-Object { $_ -match '^\w+ \d{4}$' } | Select-Object -First 1
    Check 'Widgets stay interactive' ($before -ne $after) "$before -> $after"
    Press (ByName $r 'Today' $CT::Button); Start-Sleep -Milliseconds 500
}

Start-Sleep -Seconds 6
Check 'Stays open without focus' ((Hwnd 'Glyphbar Desktop Widgets') -ne [IntPtr]::Zero)

$shell = New-Object -ComObject Shell.Application
$shell.ToggleDesktop(); Start-Sleep -Seconds 2
Gb-Shot 'desk-shown' | Out-Null
$z = [GbZOrder]::Positions($h)
Check 'Visible during Show Desktop' (((Hwnd 'Glyphbar Desktop Widgets') -ne [IntPtr]::Zero) -and ($z[0] -lt $z[1])) "widget $($z[0]) desktop $($z[1])"
$shell.ToggleDesktop(); Start-Sleep -Seconds 2
$z = [GbZOrder]::Positions($h)
Check 'Back behind apps afterwards' ($z[0] -gt $z[2]) "widget $($z[0]) app $($z[2])"

# Regression: closing the widget window while services still pointed at it crashed the app natively.
Show 'settings'
$item = All (Root 'Glyphbar Settings') $CT::ListItem | Where-Object { $_.Current.Name -eq 'Widgets' } | Select-Object -First 1
$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 900
foreach ($round in 1..3) {
    Toggle (ByName (Root 'Glyphbar Settings') 'Keep widgets on the desktop'); Start-Sleep -Seconds 1
    if ($round -eq 1) { Check 'Turning it off removes the widgets' ((Hwnd 'Glyphbar Desktop Widgets') -eq [IntPtr]::Zero) }
    Press (All (Root 'Glyphbar Settings') $CT::Button | Where-Object { $_.Current.Name -eq 'Move later' } | Select-Object -First 1); Start-Sleep -Milliseconds 700
    SetRange (ByName (Root 'Glyphbar Settings') 'Desktop widget background (%)') (10 * $round); Start-Sleep -Milliseconds 500
    Toggle (ByName (Root 'Glyphbar Settings') 'Keep widgets on the desktop'); Start-Sleep -Seconds 1.5
    Press (All (Root 'Glyphbar Settings') $CT::Button | Where-Object { $_.Current.Name -eq 'Move earlier' } | Select-Object -Last 1); Start-Sleep -Milliseconds 700
}
Check 'Survives switching widgets off and on while changing the layout' ($null -ne (Gb-Pid))
Check 'Widgets are back after switching on' ((Hwnd 'Glyphbar Desktop Widgets') -ne [IntPtr]::Zero)
Check 'Background setting is saved' ((Settings).Widgets.DesktopBackgroundPercent -eq 30) "$((Settings).Widgets.DesktopBackgroundPercent)"
SetRange (ByName (Root 'Glyphbar Settings') 'Desktop widget background (%)') 65; Start-Sleep -Milliseconds 500

# Restart interface must bring them back, and leave no stale board behind.
$sec = All (Root 'Glyphbar Settings') $CT::ListItem | Where-Object { $_.Current.Name -eq 'About' } | Select-Object -First 1
$sec.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 800
Press (ByName (Root 'Glyphbar Settings') 'Restart interface' $CT::Button); Start-Sleep -Seconds 3
Check 'Widgets return after Restart interface' ((Hwnd 'Glyphbar Desktop Widgets') -ne [IntPtr]::Zero)
Start-Process $exe -ArgumentList '--exit' -Wait; Start-Sleep -Seconds 3
Check 'Exits cleanly' (-not (Gb-Pid)) ([GbWin]::WorkArea())
$c = @(Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$t0; Id=1000} -ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'Glyphbar' })
Check 'No crashes' ($c.Count -eq 0) "$($c.Count)"
Gb-Log | Select-String ' Error | Critical |Unhandled' | Select-Object -Last 5 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(150, $_.Line.Length)) }
