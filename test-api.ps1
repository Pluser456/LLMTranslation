$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class Win34 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
    public static string EditText(IntPtr h) {
        int len = SendMessage(h, 0x000E, IntPtr.Zero, null).ToInt32();
        if (len <= 0) return "";
        StringBuilder sb = new StringBuilder(len + 1);
        SendMessage(h, 0x000D, (IntPtr)(len + 1), sb);
        return sb.ToString();
    }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WindowsOf(uint pid) {
        var list = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr p) { uint wp; GetWindowThreadProcessId(h, out wp); if (wp == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
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
}
"@
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Write-Output "STEP1_types_loaded"

# ---- mock OpenAI SSE server (separate process, tracked by PID) ----
$mockProc = Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"D:\Programs\LLMTranslation\mock-server.ps1`"" -PassThru
Start-Sleep -Seconds 1
Write-Output "STEP2_mock_started pid=$($mockProc.Id)"

# ---- config pointing to mock ----
cd D:\Programs\LLMTranslation\dist
$cfg = '{"ApiBase":"http://127.0.0.1:8899/v1","Port":"","ApiKey":"test-key","Model":"mock-model","Prompt":"translate","EnableSelection":true,"AutoStart":false}'
Set-Content config.json $cfg -Encoding UTF8
Write-Output "STEP3_config_written"

$env:LLMTRANSLATION_DEBUG = "1"
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
$tt = [IntPtr]::Zero
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Milliseconds 400
    $tt = [Win34]::FindByTitle("TestTargetApp")
    if ($tt -ne [IntPtr]::Zero) { break }
}

try {
    $tr = New-Object Win34+RECT
    [void][Win34]::GetWindowRect($tt, [ref]$tr)
    [Win34]::Click(([int](($tr.Left + $tr.Right) / 2)), $tr.Top + 10)
    Start-Sleep -Milliseconds 400

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($tt)
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $edit = $null
    foreach ($e in $all) { if ($e.Current.ClassName -like "WindowsForms10.EDIT*") { $edit = $e; break } }
    $er = $edit.Current.BoundingRectangle
    $tx = [int]$er.X + 10; $ty = [int]$er.Y + 6

    # drag select
    [Win34]::SetCursorPos($tx, $ty) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win34]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win34]::SetCursorPos($tx + 150, $ty) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win34]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700
    [void][Win34]::SetCursorPos($tx + 201, $ty + 30)
    Start-Sleep -Milliseconds 400

    # find floating and click it
    $f = $null
    foreach ($h in [Win34]::WindowsOf([uint32]$app.Id)) {
        $r = New-Object Win34+RECT
        [void][Win34]::GetWindowRect($h, [ref]$r)
        $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
        if ($w -ge 90 -and $w -le 110 -and $ht -ge 32 -and $ht -le 48) { $f = $r }
    }
    if (-not $f) { Write-Output "API_TEST_FLOATING_MISSING"; exit 1 }
    Write-Output "STEP5_floating_found"
    [Win34]::Click(([int](($f.Left + $f.Right) / 2)), ([int](($f.Top + $f.Bottom) / 2)))
    Write-Output "STEP6_clicked_floating"
    Start-Sleep -Seconds 4

    Write-Output "STEP7_reading_uia"
    foreach ($h in [Win34]::WindowsOf([uint32]$app.Id)) {
        $r2 = New-Object Win34+RECT
        [void][Win34]::GetWindowRect($h, [ref]$r2)
        Write-Output ("WIN h=" + $h + " w=" + ($r2.Right - $r2.Left) + " hgt=" + ($r2.Bottom - $r2.Top))
    }
    # read translate window textboxes via UIA ValuePattern
    $resultText = ""
    foreach ($h in [Win34]::WindowsOf([uint32]$app.Id)) {
        $r = New-Object Win34+RECT
        [void][Win34]::GetWindowRect($h, [ref]$r)
        $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
        if ($w -ge 400 -and $ht -ge 250) {
            $wr = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $eds = $wr.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            Write-Output ("CHILD_COUNT=" + $eds.Count)
            $sawWeb = $false
            foreach ($e in $eds) {
                try {
                    Write-Output ("CHILD cls=" + $e.Current.ClassName)
                    if ($e.Current.ClassName -like "Chrome_WidgetWin*" -or $e.Current.ClassName -like "EdgeWebView*") { $sawWeb = $true }
                } catch { }
                try {
                    if ($e.Current.ClassName -like "WindowsForms10.EDIT*") {
                        $hh = $e.Current.NativeWindowHandle
                        if ($hh -ne 0) { $resultText += [Win34]::EditText([IntPtr]$hh) + "`n" }
                    }
                } catch { Write-Output ("PATTERN_ERR: " + $_.Exception.Message) }
            }
        }
    }
    Write-Output ("RESULT_TEXT=[" + $resultText + "]")
    if ($sawWeb) { Write-Output "API_TEST_PASS_WEBVIEW" } elseif ($resultText -like "*Hello World!*") { Write-Output "API_TEST_PASS_TEXTBOX" } else { Write-Output "API_TEST_FAIL" }
} finally {
    Write-Output "STEP9_cleanup"
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $mockProc.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
