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
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
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
    public static void Down() { mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); }
    public static void Up() { mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
}
"@

[void][Win43]::SetProcessDPIAware()

# 读取配置里的全局热键 (如 "Ctrl+Shift+Q") 并转成 SendKeys 语法 ("^+q"); 缺省 Alt+Q
function Get-HotkeySendKeys {
    $hk = "Alt+Q"
    $cfgPath = Join-Path $env:LOCALAPPDATA "LLMTranslation\config.json"
    if (Test-Path $cfgPath) {
        try { $j = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json; if ($j.Hotkey) { $hk = $j.Hotkey } } catch { }
    }
    $pre = ""; $key = ""
    foreach ($p in ($hk -split '\+')) {
        switch ($p.Trim().ToLower()) {
            "ctrl"  { $pre += "^" }
            "shift" { $pre += "+" }
            "alt"   { $pre += "%" }
            default { $key = $p.Trim().ToLower() }
        }
    }
    if (-not $key) { throw "hotkey 无主键: $hk" }
    if ($key -match '^f([1-9]|1[0-2])$') { $key = "{$($key.ToUpper())}" }
    elseif ($key -eq "space") { $key = " " }
    elseif ($key -eq "enter") { $key = "{ENTER}" }
    elseif ($key -eq "tab") { $key = "{TAB}" }
    elseif ($key -eq "esc") { $key = "{ESC}" }
    elseif ($key.Length -ne 1 -and $key[0] -ne '{') { throw "hotkey 主键无法映射到 SendKeys: $hk" }
    $script:HotkeyRaw = $hk
    return $pre + $key
}

# 按住 (rx+ox, by+oy) 并注入 10 次 (+10,+8) 相对移动, 返回窗口物理尺寸增量
function DragDelta($h, [int] $rx, [int] $by, [int] $ox, [int] $oy) {
    [void][Win43]::SetCursorPos($rx + $ox, $by + $oy)
    Start-Sleep -Milliseconds 200
    [Win43]::Down()
    Start-Sleep -Milliseconds 100
    for ($i = 0; $i -lt 10; $i++) { [Win43]::mouse_event(0x0001, 10, 8, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
    Start-Sleep -Milliseconds 200
    [Win43]::Up()
    Start-Sleep -Milliseconds 400
    $r = New-Object Win43+RECT; [void][Win43]::GetWindowRect($h, [ref]$r)
    return $r
}

function Restore([IntPtr] $h) {
    for ($i = 0; $i -lt 20; $i++) {
        [void][Win43]::MoveWindow($h, 300, 200, 650, 500, $true)
        Start-Sleep -Milliseconds 250
        $r = New-Object Win43+RECT; [void][Win43]::GetWindowRect($h, [ref]$r)
        if ($r.Left -eq 300 -and $r.Top -eq 200 -and ($r.Right - $r.Left) -eq 650 -and ($r.Bottom - $r.Top) -eq 500) { return $r }
    }
    $r = New-Object Win43+RECT; [void][Win43]::GetWindowRect($h, [ref]$r)
    return $r
}

cd D:\Programs\LLMTranslation\dist
if (Get-Process LLMTranslation -ErrorAction SilentlyContinue) { Write-Output "ALREADY_RUNNING: 请先退出托盘实例再测试"; exit 1 }
$sk = Get-HotkeySendKeys
Write-Output ("HOTKEY=" + $script:HotkeyRaw + " -> " + $sk)
$app = Start-Process .\LLMTranslation.exe -PassThru
$target = Start-Process .\TestTarget.exe -PassThru
Start-Sleep -Seconds 2
try {
    $tt = [IntPtr]::Zero
    foreach ($h in [Win43]::WinsOf([uint32]$target.Id)) { if ([Win43]::Title($h) -eq "TestTargetApp") { $tt = $h } }
    [void][Win43]::SetForegroundWindow($tt)
    # 热键路径直接打开翻译窗 (不弹胶囊); SetForegroundWindow 可能被前台锁拒绝, 重试补发
    $win = [IntPtr]::Zero
    for ($try = 0; $try -lt 3 -and $win -eq [IntPtr]::Zero; $try++) {
        [void][Win43]::SetForegroundWindow($tt)
        Start-Sleep -Milliseconds 500
        $wsh = New-Object -ComObject WScript.Shell
        $wsh.SendKeys("^a"); Start-Sleep -Milliseconds 300
        $wsh.SendKeys($sk)
        # 轮询出现并等尺寸稳定 (流式渲染会改布局)
        $prev = ""
        for ($i = 0; $i -lt 40; $i++) {
            Start-Sleep -Milliseconds 200
            $cand = [Win43]::Big([uint32]$app.Id)
            if ($cand -ne [IntPtr]::Zero) {
                $r = New-Object Win43+RECT; [void][Win43]::GetWindowRect($cand, [ref]$r)
                $sig = "" + $r.Left + "," + $r.Top + "," + $r.Right + "," + $r.Bottom
                if ($sig -eq $prev) { $win = $cand; break }
                $prev = $sig
            } else { $prev = "" }
        }
    }
    if ($win -eq [IntPtr]::Zero) { Write-Output "NO_TRANSLATE_WINDOW"; exit 1 }
    # 固定到屏幕中部: 角落外 6px 落点避免任务栏抢捕获 / 圆角 region 外点击穿透
    $r0 = Restore $win
    Write-Output ("POS0=" + $r0.Left + "," + $r0.Top + " SIZE0=" + ($r0.Right - $r0.Left) + "x" + ($r0.Bottom - $r0.Top))

    # 标题栏拖动: 原生拖动循环只吃真实输入 (mouse_event), SetCursorPos 不产生其消息
    # DPI-aware 进程中 mouse_event 相对位移按逻辑像素, 物理增量 = 位移 x scale
    $scale = [Win43]::GetDpiForWindow($win) / 96.0
    [void][Win43]::SetCursorPos($r0.Left + 200, $r0.Top + 12)
    Start-Sleep -Milliseconds 200
    [Win43]::Down()
    Start-Sleep -Milliseconds 150
    for ($i = 0; $i -lt 12; $i++) { [Win43]::mouse_event(0x0001, -10, -4, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
    Start-Sleep -Milliseconds 200
    [Win43]::Up()
    Start-Sleep -Milliseconds 400
    $r1 = New-Object Win43+RECT; [void][Win43]::GetWindowRect($win, [ref]$r1)
    $dx = $r1.Left - $r0.Left; $dy = $r1.Top - $r0.Top
    $edx = -120 * $scale; $edy = -48 * $scale
    Write-Output ("DRAG_DELTA=" + $dx + "," + $dy + " expect=" + $edx + "," + $edy)
    if ([Math]::Abs($dx - $edx) -lt 40 -and [Math]::Abs($dy - $edy) -lt 40) { Write-Output "DRAG_OK" } else { Write-Output "DRAG_FAIL" }
    $ex = 100 * $scale; $ey = 80 * $scale

    # 1) 右下角 6px 边带交集 (-5,-5) (圆角 region 半径 12, -3 会穿透) -> 斜向缩放
    $r1 = Restore $win
    $r2 = DragDelta $win $r1.Right $r1.Bottom (-5) (-5)
    $dw = ($r2.Right - $r2.Left) - ($r1.Right - $r1.Left); $dh = ($r2.Bottom - $r2.Top) - ($r1.Bottom - $r1.Top)
    Write-Output ("RESIZE_DELTA=" + $dw + "," + $dh + " scale=" + $scale)
    if ([Math]::Abs($dw - $ex) -lt 35 -and [Math]::Abs($dh - $ey) -lt 35) { Write-Output "RESIZE_OK" } else { Write-Output "RESIZE_FAIL" }

    # 2) 角落 10px 热区、仅底边带内 (-9,-3): 修复前钩子按 6px 带算 mask -> 只能垂直; 现应双向
    $r1 = Restore $win
    $r2 = DragDelta $win $r1.Right $r1.Bottom (-9) (-3)
    $dw = ($r2.Right - $r2.Left) - ($r1.Right - $r1.Left); $dh = ($r2.Bottom - $r2.Top) - ($r1.Bottom - $r1.Top)
    Write-Output ("CORNER_DIAG_DELTA=" + $dw + "," + $dh)
    if ([Math]::Abs($dw - $ex) -lt 35 -and [Math]::Abs($dh - $ey) -lt 35) { Write-Output "CORNER_DIAG_OK" } else { Write-Output "CORNER_DIAG_FAIL" }
} finally {
    Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    Write-Output "CLEANED"
}
