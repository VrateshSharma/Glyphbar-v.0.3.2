# Measures the auto-hide slide by sampling the bar's real position about every millisecond.
# Moves the pointer for a few seconds.
. "$PSScriptRoot\ui.ps1"
if (-not ('GbSlide' -as [type])) {
Add-Type @'
using System; using System.Collections.Generic; using System.Diagnostics; using System.Runtime.InteropServices; using System.Threading;
public static class GbSlide {
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rt,B; }
  [StructLayout(LayoutKind.Sequential)] public struct P { public int X,Y; }
  [DllImport("user32")] static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32")] public static extern bool GetCursorPos(out P p);
  [DllImport("winmm")] static extern uint timeBeginPeriod(uint p);
  [DllImport("winmm")] static extern uint timeEndPeriod(uint p);
  // Moves the pointer, then returns "ms:top" for every position change until the bar settles.
  public static List<string> Watch(IntPtr bar,int x,int y,int maxMs,int bounceAfterMs,int bx,int by){
    var o=new List<string>(); var w=Stopwatch.StartNew(); R r; GetWindowRect(bar,out r); int last=r.T; long lastChange=0; bool moved=false,bounced=false;
    timeBeginPeriod(1); SetCursorPos(x,y); SetCursorPos(x+1,y);
    while(w.ElapsedMilliseconds<maxMs){ GetWindowRect(bar,out r);
      if(r.T!=last){ if(!moved){moved=true; w.Restart(); } o.Add(w.ElapsedMilliseconds+":"+r.T); last=r.T; lastChange=w.ElapsedMilliseconds; }
      if(moved&&bounceAfterMs>0&&!bounced&&w.ElapsedMilliseconds>=bounceAfterMs){ bounced=true; SetCursorPos(bx,by); SetCursorPos(bx+1,by); }
      if(moved&&w.ElapsedMilliseconds-lastChange>500&&(bounceAfterMs==0||bounced)) break;
      Thread.Sleep(1); }
    timeEndPeriod(1); return o; }
}
'@
}
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$start = New-Object GbSlide+P; [void][GbSlide]::GetCursorPos([ref]$start)
function Analyse([string]$name, $samples) {
    $tops = @($samples | ForEach-Object { [int]($_ -split ':')[1] })
    $times = @($samples | ForEach-Object { [int]($_ -split ':')[0] })
    if ($tops.Count -lt 2) {
        $script:last = @{ Frames = $tops.Count; Ms = 0; Final = $(if ($tops.Count) { $tops[-1] }); Monotonic = $true; MaxGap = 0; MaxStep = 0 }
        "$name : $($tops.Count) position change"
        return
    }
    $up = $tops[-1] -lt $tops[0]; $mono = $true; $gap = 0; $step = 0
    for ($i = 1; $i -lt $tops.Count; $i++) {
        if (($up -and $tops[$i] -gt $tops[$i - 1]) -or (-not $up -and $tops[$i] -lt $tops[$i - 1])) { $mono = $false }
        $gap = [Math]::Max($gap, $times[$i] - $times[$i - 1]); $step = [Math]::Max($step, [Math]::Abs($tops[$i] - $tops[$i - 1]))
    }
    $r = @{ Frames = $tops.Count; Ms = $times[-1]; Final = $tops[-1]; Monotonic = $mono; MaxGap = $gap; MaxStep = $step }
    "$name : $($r.Frames) frames over $($r.Ms) ms, largest gap $gap ms, largest step $step px, ends at $($r.Final)"
    $script:last = $r
}

$data = "$script:sp\gbdata"
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $data | Out-Null
$settings = '{ "SchemaVersion": 2, "Taskbar": { "AutoHide": true, "Floating": false }, "System": { "FirstRunGuideShown": true } }'
[IO.File]::WriteAllText("$data\settings.json", $settings)
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 8
[void][GbSlide]::SetCursorPos(960, 400); Start-Sleep -Seconds 2
$h = Hwnd 'Glyphbar Taskbar'
Check 'Bar starts parked' ([int]((BarRect) -split ',')[1] -ge 1076) (BarRect)

Analyse 'reveal' ([GbSlide]::Watch($h, 960, 1079, 2000, 0, 0, 0))
$r = $script:last
Check 'Reveal animates over many frames' ($r.Frames -ge 8) "$($r.Frames) frames"
Check 'Reveal only moves one way' $r.Monotonic
Check 'Reveal lands exactly' ($r.Final -eq 1028) "$($r.Final)"
Check 'Reveal takes a sensible time' ($r.Ms -ge 100 -and $r.Ms -le 450) "$($r.Ms) ms"

Analyse 'hide' ([GbSlide]::Watch($h, 960, 400, 3000, 0, 0, 0))
$r = $script:last
Check 'Hide animates over many frames' ($r.Frames -ge 8) "$($r.Frames) frames"
Check 'Hide only moves one way' $r.Monotonic
Check 'Hide lands exactly' ($r.Final -eq 1078) "$($r.Final)"
Check 'Hide has no long stalls' ($r.MaxGap -le 60) "$($r.MaxGap) ms"

# Pointer returns while the bar is on its way out.
[void][GbSlide]::SetCursorPos(960, 1079); [void][GbSlide]::SetCursorPos(962, 1079); Start-Sleep -Seconds 1
Analyse 'interrupted' ([GbSlide]::Watch($h, 960, 400, 3000, 90, 960, 1079))
$r = $script:last
Check 'Pointer returning mid-hide brings it back' ($r.Final -eq 1028) "$($r.Final)"
Check 'The reversal does not jump' ($r.MaxStep -le 30) "$($r.MaxStep) px"

Start-Process $exe -ArgumentList '--exit' -Wait; Start-Sleep -Seconds 3
Check 'Exits cleanly' (-not (Gb-Pid)) ([GbWin]::WorkArea())

# With animation off, the bar must simply jump.
[IO.File]::WriteAllText("$data\settings.json", '{ "SchemaVersion": 2, "Appearance": { "Animation": "Off" }, "Taskbar": { "AutoHide": true, "Floating": false }, "System": { "FirstRunGuideShown": true } }')
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 8
[void][GbSlide]::SetCursorPos(960, 400); Start-Sleep -Seconds 2
$h = Hwnd 'Glyphbar Taskbar'
Analyse 'animation off' ([GbSlide]::Watch($h, 960, 1079, 1500, 0, 0, 0))
Check 'Animation off means no slide' ($script:last.Frames -le 2) "$($script:last.Frames) frames"
[void][GbSlide]::SetCursorPos($start.X, $start.Y)
Start-Process $exe -ArgumentList '--exit' -Wait; Start-Sleep -Seconds 3
Check 'Exits cleanly again' (-not (Gb-Pid)) ([GbWin]::WorkArea())
