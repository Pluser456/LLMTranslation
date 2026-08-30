$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class Win46 {
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
    public static Win46.RECT Big(uint pid) {
        foreach (var h in WinsOf(pid)) {
            var r = new Win46.RECT(); GetWindowRect(h, out r);
            if (r.Right - r.Left >= 400 && r.Bottom - r.Top >= 220) return r;
        }
        return new Win46.RECT();
    }
}
"@

function Shot($rect, $name) {
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $bmp.Save("D:\Programs\LLMTranslation\$name", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

cd D:\Programs\LLMTranslation\dist
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [Win46]::WinsOf([uint32]$target.Id)) { if ([Win46]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][Win46]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 500
    $wsh = New-Object -ComObject WScript.Shell
    $wsh.SendKeys("^a"); Start-Sleep -Milliseconds 300
    $wsh.SendKeys("%q")
    $f = $null
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Milliseconds 200
        foreach ($h in [Win46]::WinsOf([uint32]$app.Id)) {
            $r = New-Object Win46+RECT; [void][Win46]::GetWindowRect($h, [ref]$r)
            $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
            if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { $f = $r }
        }
        if ($f) { [void][Win46]::SetCursorPos(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2))); Start-Sleep -Milliseconds 200; break }
    }
    if (-not $f) { Write-Output "NO_FLOATING"; exit 1 }
    [Win46]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win46]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 900
    $r1 = [Win46]::Big([uint32]$app.Id); Shot $r1 "shot_mid.png"
    Start-Sleep -Milliseconds 1200
    $r2 = [Win46]::Big([uint32]$app.Id); Shot $r2 "shot_mid2.png"
    Start-Sleep -Seconds 3
    $r3 = [Win46]::Big([uint32]$app.Id); Shot $r3 "shot_end.png"
    Write-Output "SHOTS_DONE"
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
