$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinF {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WinsOf(uint pid) {
        var l = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr p) { uint wp; GetWindowThreadProcessId(h, out wp); if (wp == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
        return l;
    }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
"@
cd D:\Programs\LLMTranslation\dist
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [WinF]::WinsOf([uint32]$target.Id)) { if ([WinF]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][WinF]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 400
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a")
    Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")
    $f = $null
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 200
        foreach ($h in [WinF]::WinsOf([uint32]$app.Id)) {
            $r = New-Object WinF+RECT; [void][WinF]::GetWindowRect($h, [ref]$r)
            $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
            if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { $f = $r }
        }
        if ($f) { break }
    }
    if (-not $f) { Write-Output "NO_FLOATING"; exit 1 }
    # capture with a margin, scaled x4
    $pad = 6
    $x = $f.Left - $pad; $y = $f.Top - $pad; $w = ($f.Right - $f.Left) + 2*$pad; $h = ($f.Bottom - $f.Top) + 2*$pad
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $big = New-Object System.Drawing.Bitmap ($w*4), ($h*4)
    $g2 = [System.Drawing.Graphics]::FromImage($big)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g2.DrawImage($bmp, 0, 0, ($w*4), ($h*4))
    $big.Save("D:\Programs\LLMTranslation\float_zoom.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $g2.Dispose(); $big.Dispose()
    Write-Output "FLOAT_SHOT"
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
