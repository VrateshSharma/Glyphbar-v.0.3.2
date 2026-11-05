# Test helpers for driving Glyphbar on the real desktop.
$script:sp = Join-Path $env:TEMP 'glyphbar-e2e'
New-Item -ItemType Directory -Force $script:sp | Out-Null
$script:exe = Join-Path $PSScriptRoot '..\..\src\Glyphbar.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\Glyphbar.exe'
$env:GLYPHBAR_DATA_DIR = "$script:sp\gbdata"
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
if (-not ('GbWin' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text; using System.Collections.Generic;
public static class GbWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [DllImport("user32")] public static extern bool SystemParametersInfo(int a,int b,ref RECT r,int c);
  [DllImport("user32")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32",CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
  [DllImport("user32")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
  public static string WorkArea(){ var r=new RECT(); SystemParametersInfo(0x30,0,ref r,0); return r.L+","+r.T+","+r.R+","+r.B; }
  public static List<string> Windows(int pid){ var o=new List<string>();
    EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p); if(p==pid){ RECT r; GetWindowRect(h,out r); var s=new StringBuilder(200); GetWindowText(h,s,200);
      o.Add(string.Format("{0} vis={1} [{2},{3},{4},{5}] '{6}'",h,IsWindowVisible(h),r.L,r.T,r.R,r.B,s)); } return true; },IntPtr.Zero); return o; }
  public static int BroadcastTo(int pid, string msg){ uint m=RegisterWindowMessage(msg); int n=0;
    EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p); if(p==pid){ IntPtr r; SendMessageTimeout(h,m,IntPtr.Zero,IntPtr.Zero,2,2000,out r); n++; } return true; },IntPtr.Zero); return n; }
}
'@
}
function Gb-Shot([string]$name, [int]$h = 0) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $top = if ($h -gt 0) { $b.Bottom - $h } else { $b.Top }
    $hh = if ($h -gt 0) { $h } else { $b.Height }
    $bmp = New-Object System.Drawing.Bitmap($b.Width, $hh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Left, $top, 0, 0, $bmp.Size)
    $bmp.Save("$script:sp\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "$script:sp\$name.png"
}
function Gb-Log { Get-ChildItem "$script:sp\gbdata\logs" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1 | ForEach-Object { Get-Content $_.FullName -Tail 40 } }
# A crashed process can linger with no threads; it isn't running, so it doesn't count.
function Gb-Pid { (Get-Process Glyphbar -ErrorAction SilentlyContinue | Where-Object { $_.Threads.Count -gt 0 } | Select-Object -First 1).Id }
