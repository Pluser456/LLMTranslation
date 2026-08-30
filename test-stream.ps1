$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class Win45 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
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
    foreach ($h in [Win45]::WinsOf([uint32]$target.Id)) { if ([Win45]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][Win45]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 500
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a"); Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")
    $f = $null
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 200
        foreach ($h in [Win45]::WinsOf([uint32]$app.Id)) {
            $r = New-Object Win45+RECT; [void][Win45]::GetWindowRect($h, [ref]$r)
            $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
            if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { $f = $r }
        }
        if ($f) { [void][Win45]::SetCursorPos(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2))); Start-Sleep -Milliseconds 200; break }
    }
    if (-not $f) { Write-Output "NO_FLOATING"; exit 1 }
    [Win45]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win45]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Write-Output "CLICKED, waiting 10s for stream..."
    Start-Sleep -Seconds 10
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
