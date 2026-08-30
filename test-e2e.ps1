$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class Win32 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int n);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WindowsOf(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr p) { uint wp; GetWindowThreadProcessId(h, out wp); if (wp == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    public static string ClassName(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static IntPtr FindByTitle(string title) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) { if (IsWindowVisible(h) && Title(h) == title) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
    public static void Click(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(120);
    }
    public static void DblClick(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(100);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(100);
    }
}
"@
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

function Find-Floating($procId) {
    foreach ($h in [Win32]::WindowsOf([uint32]$procId)) {
        $r = New-Object Win32+RECT
        [void][Win32]::GetWindowRect($h, [ref]$r)
        $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
        if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { return $r }
    }
    return $null
}
function Find-Big($procId) {
    foreach ($h in [Win32]::WindowsOf([uint32]$procId)) {
        $r = New-Object Win32+RECT
        [void][Win32]::GetWindowRect($h, [ref]$r)
        $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
        if ($w -ge 400 -and $ht -ge 250) { return $true }
    }
    return $false
}

cd D:\Programs\LLMTranslation\dist
$env:LLMTRANSLATION_DEBUG = "1"
Remove-Item log.txt -ErrorAction SilentlyContinue
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
$tt = [IntPtr]::Zero
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Milliseconds 400
    $tt = [Win32]::FindByTitle("TestTargetApp")
    if ($tt -ne [IntPtr]::Zero) { break }
}
if ($tt -eq [IntPtr]::Zero) { Write-Output "TARGET_MISSING"; Stop-Process -Id $app.Id -Force; exit 1 }

try {
    [void][Win32]::SetForegroundWindow($tt)
    Start-Sleep -Milliseconds 500

    # locate the edit control rectangle via UIAutomation
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($tt)
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $edit = $null
    foreach ($e in $all) { if ($e.Current.ClassName -like "WindowsForms10.EDIT*") { $edit = $e; break } }
    if ($edit -eq $null) { Write-Output "EDIT_CONTROL_MISSING"; exit 1 }
    $er = $edit.Current.BoundingRectangle
    $tx = [int]$er.X + 10; $ty = [int]$er.Y + 6

    # ---- TEST 1: drag selection ----
    [Win32]::SetCursorPos($tx, $ty) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win32]::SetCursorPos($tx + 150, $ty) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700
    [void][Win32]::SetCursorPos($tx + 201, $ty + 30)
    Start-Sleep -Milliseconds 400
    $f = Find-Floating $app.Id
    if ($f) { Write-Output "TEST1_DRAG_FLOATING_FOUND" } else { Write-Output "TEST1_DRAG_FLOATING_MISSING" }

    if ($f) {
        [Win32]::Click(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2)))
        Start-Sleep -Seconds 2
        if (Find-Big $app.Id) { Write-Output "TEST1_TRANSLATE_WINDOW_FOUND" } else { Write-Output "TEST1_TRANSLATE_WINDOW_MISSING" }
    }

    # ---- TEST 2: double click selection ----
    $wsh2 = New-Object -ComObject WScript.Shell
    $wsh2.SendKeys("{ESC}")   # close translate window (it has focus)
    Start-Sleep -Milliseconds 400
    # activate target by clicking its title bar (real input bypasses foreground lock)
    $tr = New-Object Win32+RECT
    [void][Win32]::GetWindowRect($tt, [ref]$tr)
    [Win32]::Click(([int](($tr.Left + $tr.Right) / 2)), $tr.Top + 10)
    Start-Sleep -Milliseconds 400
    [Win32]::DblClick($tx + 100, $ty)
    Start-Sleep -Milliseconds 700
    [void][Win32]::SetCursorPos($tx + 151, $ty + 30)
    Start-Sleep -Milliseconds 400
    $f2 = Find-Floating $app.Id
    if ($f2) { Write-Output "TEST2_DBLCLICK_FLOATING_FOUND" } else { Write-Output "TEST2_DBLCLICK_FLOATING_MISSING" }
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
    if (Test-Path log.txt) { Get-Content log.txt | Select-Object -Last 40 }
}
