$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinE {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WinsOf(uint pid) {
        var l = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr p) { uint wp; GetWindowThreadProcessId(h, out wp); if (wp == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
        return l;
    }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static IntPtr Big(uint pid) {
        foreach (var h in WinsOf(pid)) {
            var r = new RECT(); GetWindowRect(h, out r);
            if (r.Right - r.Left >= 400 && r.Bottom - r.Top >= 220) return h;
        }
        return IntPtr.Zero;
    }
    public static RECT Rect(IntPtr h) { var r = new RECT(); GetWindowRect(h, out r); return r; }
    public static void Down() { mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); }
    public static void Up() { mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
}
"@
cd D:\Programs\LLMTranslation\dist
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [WinE]::WinsOf([uint32]$target.Id)) { if ([WinE]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][WinE]::SetCursorPos(0,0)
    [void][WinE]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 400
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a"); Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")
    $f = $null
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 200
        foreach ($h in [WinE]::WinsOf([uint32]$app.Id)) {
            $r = [WinE]::Rect($h); $bw = $r.Right-$r.Left; $bh = $r.Bottom-$r.Top
            if ($bw -ge 90 -and $bw -le 110 -and $bh -ge 32 -and $bh -le 48) { $f = $r }
        }
        if ($f) { [void][WinE]::SetCursorPos(([int](($f.Left+$f.Right)/2)), ([int](($f.Top+$f.Bottom)/2))); Start-Sleep -Milliseconds 200; [WinE]::Down(); [WinE]::Up(); break }
    }
    Start-Sleep -Milliseconds 900
    $w = [WinE]::Big([uint32]$app.Id)
    if ($w -eq [IntPtr]::Zero) { Write-Output "NO_WINDOW"; exit 1 }
    function Drag($x,$y,$dx,$dy) {
        $x=[int]$x; $y=[int]$y; $dx=[int]$dx; $dy=[int]$dy
        [void][WinE]::SetCursorPos($x,$y); Start-Sleep -Milliseconds 150
        [WinE]::Down(); Start-Sleep -Milliseconds 100
        for ($s = 1; $s -le 6; $s++) {
            $nx = [int]($x + $dx * $s / 6); $ny = [int]($y + $dy * $s / 6)
            [void][WinE]::SetCursorPos($nx, $ny); Start-Sleep -Milliseconds 60
        }
        [WinE]::Up(); Start-Sleep -Milliseconds 200
    }
    $r0 = [WinE]::Rect($w); $w0 = $r0.Right-$r0.Left; $h0 = $r0.Bottom-$r0.Top
    # right edge middle
    Drag ($r0.Right-3) ([int](($r0.Top+$r0.Bottom)/2)) 60 0
    $r1 = [WinE]::Rect($w); $wR = $r1.Right-$r1.Left
    # bottom edge middle
    Drag ([int](($r1.Left+$r1.Right)/2)) ($r1.Bottom-3) 0 50
    $r2 = [WinE]::Rect($w); $hB = $r2.Bottom-$r2.Top
    # left edge middle
    Drag ($r2.Left+3) ([int](($r2.Top+$r2.Bottom)/2)) -40 0
    $r3 = [WinE]::Rect($w); $wL = $r3.Right-$r3.Left
    # top edge middle
    Drag ([int](($r3.Left+$r3.Right)/2)) ($r3.Top+3) 0 -30
    $r4 = [WinE]::Rect($w); $hT = $r4.Bottom-$r4.Top
    Write-Output ("W0={0} H0={1}" -f $w0,$h0)
    Write-Output ("RIGHT: {0}->{1} {2}" -f $w0,$wR, $(if($wR -gt $w0+30){"OK"}else{"FAIL"}))
    Write-Output ("BOTTOM: {0}->{1} {2}" -f $h0,$hB, $(if($hB -gt $h0+20){"OK"}else{"FAIL"}))
    Write-Output ("LEFT: {0}->{1} {2}" -f $wR,$wL, $(if($wL -gt $wR+10){"OK"}else{"FAIL"}))
    Write-Output ("TOP: {0}->{1} {2}" -f $hB,$hT, $(if($hT -gt $hB+10){"OK"}else{"FAIL"}))
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
