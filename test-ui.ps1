$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class Win43 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WinsOf(uint pid) {
        var l = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr p) { uint wp; GetWindowThreadProcessId(h, out wp); if (wp == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
        return l;
    }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static void Down() { mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); }
    public static void Up() { mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
    public static Win43.RECT Big(uint pid) {
        foreach (var h in WinsOf(pid)) {
            var r = new Win43.RECT(); GetWindowRect(h, out r);
            if (r.Right - r.Left >= 400 && r.Bottom - r.Top >= 220) return r;
        }
        return new Win43.RECT();
    }
}
"@

[void][Win43]::SetProcessDPIAware()

cd D:\Programs\LLMTranslation\dist
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [Win43]::WinsOf([uint32]$target.Id)) { if ([Win43]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][Win43]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 500
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a"); Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")

    # poll for floating (it auto-hides), hover it to keep alive
    $f = $null
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 200
        foreach ($h in [Win43]::WinsOf([uint32]$app.Id)) {
            $r = New-Object Win43+RECT; [void][Win43]::GetWindowRect($h, [ref]$r)
            $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
            if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { $f = $r }
        }
        if ($f) { [void][Win43]::SetCursorPos(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2))); Start-Sleep -Milliseconds 200; break }
    }
    if (-not $f) { Write-Output "NO_FLOATING"; exit 1 }
    [void][Win43]::SetCursorPos(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2)))
    Start-Sleep -Milliseconds 200; [Win43]::Down(); [Win43]::Up()
    Start-Sleep -Seconds 2

    $r0 = [Win43]::Big([uint32]$app.Id)
    Write-Output ("POS0=" + $r0.Left + "," + $r0.Top + " SIZE0=" + ($r0.Right - $r0.Left) + "x" + ($r0.Bottom - $r0.Top))

    # drag by title bar (leftward to stay on screen)
    [void][Win43]::SetCursorPos($r0.Left + 200, $r0.Top + 12)
    Start-Sleep -Milliseconds 200
    [Win43]::Down()
    [void][Win43]::SetCursorPos($r0.Left + 80, $r0.Top - 28)
    Start-Sleep -Milliseconds 200
    [Win43]::Up()
    Start-Sleep -Milliseconds 400
    $r1 = [Win43]::Big([uint32]$app.Id)
    $dx = $r1.Left - $r0.Left; $dy = $r1.Top - $r0.Top
    Write-Output ("DRAG_DELTA=" + $dx + "," + $dy)
    if ([Math]::Abs($dx + 120) -lt 10 -and [Math]::Abs($dy + 40) -lt 10) { Write-Output "DRAG_OK" } else { Write-Output "DRAG_FAIL" }

    # resize by grip
    Write-Output ("GRIP_CLICK=" + ($r1.Right - 8) + "," + ($r1.Bottom - 8))
    [void][Win43]::SetCursorPos($r1.Right - 3, $r1.Bottom - 3)
    Start-Sleep -Milliseconds 200
    [Win43]::Down()
    Start-Sleep -Milliseconds 100
    # inject relative moves (SetCursorPos doesn't generate LL-hook events)
    for ($i = 0; $i -lt 10; $i++) { [Win43]::mouse_event(0x0001, 10, 8, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
    Start-Sleep -Milliseconds 200
    [Win43]::Up()
    Start-Sleep -Milliseconds 400
    $r2 = [Win43]::Big([uint32]$app.Id)
    $dw = ($r2.Right - $r2.Left) - ($r1.Right - $r1.Left)
    $dh = ($r2.Bottom - $r2.Top) - ($r1.Bottom - $r1.Top)
    Write-Output ("RESIZE_DELTA=" + $dw + "," + $dh)
    if ([Math]::Abs($dw - 100) -lt 35 -and [Math]::Abs($dh - 80) -lt 35) { Write-Output "RESIZE_OK" } else { Write-Output "RESIZE_FAIL" }
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
