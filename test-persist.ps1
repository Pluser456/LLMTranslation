$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinP {
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
    public static bool HasFloat(uint pid) {
        foreach (var h in WinsOf(pid)) {
            var r = new RECT(); GetWindowRect(h, out r);
            int w = r.Right - r.Left, ht = r.Bottom - r.Top;
            if (w >= 90 && w <= 140 && ht >= 32 && ht <= 70) return true;
        }
        return false;
    }
}
"@
cd D:\Programs\LLMTranslation\dist
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [WinP]::WinsOf([uint32]$target.Id)) { if ([WinP]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][WinP]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 400
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a")
    Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")
    $found = $false
    for ($i = 0; $i -lt 15; $i++) { Start-Sleep -Milliseconds 200; if ([WinP]::HasFloat([uint32]$app.Id)) { $found = $true; break } }
    if (-not $found) { Write-Output "NO_FLOATING"; exit 1 }
    Write-Output "APPEARED"
    Start-Sleep -Seconds 3
    if ([WinP]::HasFloat([uint32]$app.Id)) { Write-Output "PERSIST_OK" } else { Write-Output "PERSIST_FAIL" }
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
