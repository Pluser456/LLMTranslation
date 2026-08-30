# =====================================================================
# LLM 划词翻译 - 自动化检查脚本 (v12)
# 用法: 双击 run-repro.bat (不要直接双击本 .ps1)
# 流程: 前置检查 -> 开设置窗 -> 拖动设置窗(验证拖拽) -> 关设置窗 -> 判定鼠标钩子
# 全程约 60 秒, 期间鼠标会被自动控制, 请不要动鼠标
# =====================================================================
$ErrorActionPreference = "Continue"

# ---------- 关键: 让本脚本以系统 DPI 感知运行 ----------
# 本机曾出现"无感知进程被按旧 125% 虚拟化 (0.8x)"的陈旧状态, 坐标会整体错位 => 必须先声明感知
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DQ2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[void][DQ2]::SetProcessDPIAware()

# ---------- 鼠标控制 + 窗口枚举 ----------
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class MI {
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public MOUSE mi; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSE { public int dx, dy; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] a, int sz);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr id);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr id);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  public static void Move(int x, int y) {
    var i = new INPUT(); i.type = 0;
    i.mi.dx = (x * 65535) / 1920; i.mi.dy = (y * 65535) / 1080;
    i.mi.flags = 0x8001;
    SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
  }
  public static void Btn(uint f) {
    var i = new INPUT(); i.type = 0; i.mi.flags = f;
    SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
  }
  public static void Click(int x, int y, int gap) { Move(x, y); Btn(0x2); System.Threading.Thread.Sleep(gap); Btn(0x4); }
  public static void DblClick(int x, int y) {
    Move(x, y); Btn(0x2); System.Threading.Thread.Sleep(40); Btn(0x4);
    System.Threading.Thread.Sleep(80);
    Btn(0x2); System.Threading.Thread.Sleep(40); Btn(0x4);
  }
  public static void DragTo(int x1, int y1, int x2, int y2) {
    Move(x1, y1); Btn(0x2); System.Threading.Thread.Sleep(80);
    int steps = 8;
    for (int i = 1; i <= steps; i++) {
      Move(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps);
      System.Threading.Thread.Sleep(30);
    }
    Btn(0x4);
  }
  // 找到指定进程的最大可见顶层窗口 (设置窗) 的 RECT; 未找到返回 null
  public static int[] FindLargestVisibleWindow(int pid) {
    IntPtr best = IntPtr.Zero;
    int bw = 0;
    EnumWindows(delegate(IntPtr h, IntPtr id)
    {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if ((int)p != pid) return true;
      if (!IsWindowVisible(h)) return true;
      RECT r;
      GetWindowRect(h, out r);
      int w = r.r - r.l, hh = r.b - r.t;
      if (w > 100 && hh > 100 && w > bw) { bw = w; best = h; }
      return true;
    }, IntPtr.Zero);
    if (best == IntPtr.Zero) return null;
    RECT r;
    GetWindowRect(best, out r);
    return new int[] { r.l, r.t, r.r, r.b };
  }
}
"@

# ---------- 配置 (如托盘图标位置不对, 改这里) ----------
$trayX = 1617; $trayY = 1054    # 托盘图标 (日志确认过的位置)
$testX = 960;  $testY = 60      # 测试点击位置 (屏幕顶部中央, 通常是空白桌面)

$DataRoot = Join-Path $env:LOCALAPPDATA "LLMTranslation"
$Log = Join-Path $DataRoot "log.txt"
$ResultFile = Join-Path $PSScriptRoot "repro-result.txt"
$report = @()
function Say($m) { Write-Host $m; $script:report += $m }
function Note($m) { Write-Host ("    " + $m) }
function CountDown($sec, $msg) {
  Write-Host (">>> " + $msg)
  $script:report += (">>> " + $msg)
  for ($i = $sec; $i -ge 1; $i--) { Write-Host ("    {0} ..." -f $i); Start-Sleep -Seconds 1 }
}
function SaveReport {
  $now = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
  Set-Content -Path $ResultFile -Value ("时间: " + $now + "`n" + ($report -join "`n")) -Encoding UTF8
}
function ReadLog {
  for ($i = 0; $i -lt 10; $i++) {
    try { return (Get-Content $Log -ErrorAction Stop) } catch { Start-Sleep -Milliseconds 200 }
  }
  return @()
}
function Mark { $l = ReadLog; if ($l) { $l.Count } else { 0 } }
function NewLbdn($before) {
  $lines = ReadLog
  if (-not $lines) { return @() }
  if ($before -ge $lines.Count) { $before = 0 }
  return @($lines | Select-Object -Skip $before | Where-Object { $_ -match "LBDN" })
}
function TestHookLiveness($label) {
  $m = Mark
  [MI]::Click($testX, $testY, 60)
  Start-Sleep -Seconds 2
  $hits = NewLbdn $m
  if ($hits.Count -gt 0) { Say ("    [钩子存活] {0} -> {1}" -f $label, $hits[0]); $true }
  else { Say ("    [钩子失聪] {0} -> 点击后无任何 LBDN 日志" -f $label); $false }
}

# ---------- Step 0: 前置 ----------
Say "== Step 0: 前置检查 =="
if (-not (Test-Path $DataRoot)) { New-Item -ItemType Directory -Path $DataRoot | Out-Null }
$flag = Join-Path $DataRoot "debug.txt"
if (-not (Test-Path $flag)) { New-Item -ItemType File -Path $flag | Out-Null; Note "已创建 debug.txt (开启调试日志, 用完可删)" }
else { Note "调试日志已开启" }

$proc = Get-Process -Name "LLMTranslation" -ErrorAction SilentlyContinue
if (-not $proc) {
  Note "应用未运行, 尝试启动..."
  $candidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\LLMTranslation\LLMTranslation.exe"),
    "D:\Programs\LLMTranslation\dist\LLMTranslation.exe"
  )
  $started = $false
  foreach ($c in $candidates) { if (Test-Path $c) { Start-Process $c; $started = $true; Note ("已启动: " + $c); break } }
  if (-not $started) { Say "!! 找不到可执行文件, 请先手动启动应用后重新运行本脚本."; SaveReport; Read-Host "按回车退出" | Out-Null; exit 1 }
  Start-Sleep -Seconds 6
} else {
  $ver = (Select-String -Path $Log -Pattern "build=(\w+)" | Select-Object -Last 1)
  Note ("应用已在运行 (PID {0})" -f $proc.Id)
}
Start-Sleep -Seconds 2

$baseline = TestHookLiveness "基线(开设置窗前)"
if (-not $baseline) {
  Say "!! 基线即失聪: 鼠标钩子当前不工作 (可能此前已触发过故障)。请重启应用后重跑。"
  SaveReport
  Read-Host "按回车退出" | Out-Null
  exit 1
}

# ---------- Step 1: 打开设置窗 ----------
CountDown 5 "双击任务栏托盘图标, 打开 设置/使用说明 窗口"
$m1 = Mark
[MI]::DblClick($trayX, $trayY)
Note "已双击托盘, 等待 14 秒 (WebView2 冷启动 + 页面加载)..."
Start-Sleep -Seconds 14
$hits1 = NewLbdn $m1
Note ("双击期间钩子收到事件数: " + $hits1.Count)
if ($hits1.Count -eq 0) {
  Say "!! 双击托盘期间无钩子事件 — 托盘图标位置可能不对 (当前 $trayX,$trayY), 请核对后修改脚本顶部再运行。"
  SaveReport
  Read-Host "按回车退出" | Out-Null
  exit 1
}

# ---------- Step 2: 对照 (设置窗打开时) ----------
CountDown 3 "点击桌面空白处 (对照: 设置窗打开时钩子应仍存活)"
$ok2 = TestHookLiveness "设置窗打开时"

# ---------- Step 3: 拖动设置窗 (验证手动拖拽修复) ----------
CountDown 3 "拖动设置窗顶条向右 80px (验证拖拽功能)"
$rect = [MI]::FindLargestVisibleWindow($proc.Id)
if ($rect) {
  $dx0 = $rect[0]
  [MI]::DragTo($rect[0] + 100, $rect[1] + 20, $rect[0] + 180, $rect[1] + 20)
  Start-Sleep -Seconds 1
  $rect2 = [MI]::FindLargestVisibleWindow($proc.Id)
  if ($rect2 -and $rect2[0] -ne $dx0) {
    Say ("    [拖拽正常] 窗口 X: {0} -> {1}" -f $dx0, $rect2[0])
  } else {
    Say "    [拖拽失败] 窗口位置未变化 — 请检查版本是否为 v12 (含手动拖拽修复)"
  }
} else {
  Note "未找到设置窗窗口 (可能已关闭), 跳过拖拽测试"
}
Start-Sleep -Seconds 1

# ---------- Step 4: 关闭设置窗 ----------
CountDown 3 "点击设置窗右上角 X 按钮关闭 (共享 WebView2 环境, Chromium 进程不退出)"
$rect3 = [MI]::FindLargestVisibleWindow($proc.Id)
if ($rect3) {
  $m3 = Mark
  [MI]::Click($rect3[2] - 30, $rect3[1] + 20, 60)
} else {
  # 兜底: 按 100% 缩放居中坐标点 X
  $m3 = Mark
  [MI]::Click(1190, 232, 60)
}
Start-Sleep -Seconds 4
Note "设置窗已关闭"

# ---------- Step 5: 判定 ----------
Say "== Step 5: 关闭后钩子判定 =="
$ok4 = TestHookLiveness "关闭设置窗后"
$deaf = -not $ok4
if ($deaf) {
  Note "第二次确认..."
  Start-Sleep -Seconds 1
  $ok4b = TestHookLiveness "关闭设置窗后(确认)"
  $deaf = -not $ok4b
}

Say ""
Say "================ 结果 ================"
if ($deaf) {
  Say ">>> 复现: 关闭设置窗后, 应用鼠标钩子失聪 — 划词胶囊不会再弹出。"
  Say ">>> 恢复方法: 重启 LLM 划词翻译 (托盘->退出, 再启动)。请把 repro-result.txt 发给我。"
} else {
  Say ">>> 通过: 开/拖/关设置窗全程, 鼠标钩子保持正常。"
}
Say "======================================="
SaveReport
Note ("详细报告已写入: " + $ResultFile)
Read-Host "按回车退出" | Out-Null
