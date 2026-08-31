# 翻译窗最小化/任务栏恢复 端到端测试 (阶段1: 打开翻译窗并截取标题栏)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Text; using System.Drawing; using System.Runtime.InteropServices; using System.Collections.Generic;
public class W {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EW cb, IntPtr l);
  public delegate bool EW(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public struct RECT { public int L,T,R,B; }
  public static IntPtr Find(string t) {
    IntPtr hit = IntPtr.Zero;
    EnumWindows((h,l)=>{ var sb=new StringBuilder(256); GetWindowTextW(h,sb,256);
      if (IsWindowVisible(h) && sb.ToString()==t) { hit=h; return false; } return true; }, IntPtr.Zero);
    return hit;
  }
  public static bool Iconic(IntPtr h) { return IsIconic(h); }
  public static RECT R(IntPtr h) { RECT r; GetWindowRect(h, out r); return r; }
}
'@
function Drag($x1,$y1,$x2,$y2) {
  [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($x1,$y1); Start-Sleep -m 150
  [System.Windows.Forms.SendKeys]::SendWait("^c") | Out-Null  # noop placeholder removed below
}
# --- 用 mouse_event 做真实拖拽 ---
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  public const uint DOWN=0x0002, UP=0x0004, MOVE=0x0001;
  public static void Click(int x, int y) { SetCursorPos(x,y); System.Threading.Thread.Sleep(120); mouse_event(DOWN,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(UP,0,0,0,0); }
  public static void DragSel(int x1,int y1,int x2,int y2) {
    SetCursorPos(x1,y1); System.Threading.Thread.Sleep(120);
    mouse_event(DOWN,0,0,0,0); System.Threading.Thread.Sleep(80);
    for (int i=1;i<=10;i++){ SetCursorPos(x1+(x2-x1)*i/10, y1+(y2-y1)*i/10); System.Threading.Thread.Sleep(25);}
    mouse_event(UP,0,0,0,0);
  }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
'@
# 1) 启动 TestTarget
if (-not (Get-Process TestTarget -ErrorAction SilentlyContinue)) { Start-Process "D:\Programs\LLMTranslation\dist\TestTarget.exe"; Start-Sleep 3 }
$tt = [W]::Find('TestTargetApp')
if ($tt -eq [IntPtr]::Zero) { Write-Host "NO TESTTARGET"; exit 1 }
[void][W]::SetForegroundWindow($tt); Start-Sleep -m 400
$r = [W]::R($tt); Write-Host ("TestTarget rect=" + $r.L + "," + $r.T + "," + $r.R + "," + $r.B)
# 2) 拖选出胶囊
[M]::DragSel($r.L+60, $r.T+110, $r.L+300, $r.T+110)
Start-Sleep -m 900
$cap = [W]::Find('LLMT_FLOAT')
if ($cap -eq [IntPtr]::Zero) { Write-Host "NO CAPSULE"; exit 1 }
$cr = [W]::R($cap); Write-Host ("capsule=" + $cr.L + "," + $cr.T + "," + $cr.R + "," + $cr.B)
# 3) 点胶囊 → 翻译窗
[M]::Click(($cr.L+$cr.R)/2, (($cr.T+$cr.B)/2))
Start-Sleep 2
$tr = [W]::Find('LLM 划词翻译')
if ($tr -eq [IntPtr]::Zero) { Write-Host "NO TRANSLATE"; exit 1 }
$w = [W]::R($tr); Write-Host ("translate=" + $w.L + "," + $w.T + "," + $w.R + "," + $w.B)
# 4) 截标题栏条带 (右上角 260x56)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(260, 56)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(($w.R-260), $w.T, 0, 0, (New-Object System.Drawing.Size(260,56)))
$bmp.Save("D:\Programs\LLMTranslation\flipprobe\hdr.png")
Write-Host ("SHOT " + $w.L + " " + $w.T + " " + $w.R + " " + $w.B)
