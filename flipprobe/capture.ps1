# 胶囊 A/B 截图: 在 TestTarget 内拖选文字 -> 等胶囊弹出 -> 全屏截图
# (本系统 FindWindow 不可用, 改用 EnumWindows 枚举)
param([string]$Out = "shot.png")

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition '
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win32Cap2 {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, uint i);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'

function Find-TopWindow([string]$title) {
    $script:fw_found = [IntPtr]::Zero
    $cb = [Win32Cap2+EnumProc]{
        param($h, $p)
        if ($script:fw_found -ne [IntPtr]::Zero) { return $true }
        $sb = New-Object System.Text.StringBuilder 512
        [void][Win32Cap2]::GetWindowTextW($h, $sb, 512)
        if ($sb.Length -gt 0 -and $sb.ToString() -eq $title -and [Win32Cap2]::IsWindowVisible($h)) { $script:fw_found = $h }
        return $true
    }
    [void][Win32Cap2]::EnumWindows($cb, [IntPtr]::Zero)
    return $script:fw_found
}

$h = Find-TopWindow "TestTargetApp"
if ($h -eq [IntPtr]::Zero) {
    Start-Process "D:\Programs\LLMTranslation\dist\TestTarget.exe"
    for ($i = 0; $i -lt 12; $i++) {
        Start-Sleep -Milliseconds 500
        $h = Find-TopWindow "TestTargetApp"
        if ($h -ne [IntPtr]::Zero) { break }
    }
    if ($h -eq [IntPtr]::Zero) { throw "TestTarget 未启动" }
}
[Win32Cap2]::ShowWindow($h, 9) | Out-Null
Start-Sleep -Milliseconds 300
[Win32Cap2]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

$r = New-Object Win32Cap2+RECT
[Win32Cap2]::GetWindowRect($h, [ref]$r) | Out-Null
Write-Output ("TestTarget rect: Left={0} Top={1} Right={2} Bottom={3}" -f $r.Left, $r.Top, $r.Right, $r.Bottom)

# 在文本区拖选一段 (绿色背景区中部)
$sx = $r.Left + 70; $sy = $r.Top + 90
[Win32Cap2]::SetCursorPos($sx, $sy) | Out-Null
Start-Sleep -Milliseconds 200
[Win32Cap2]::mouse_event(0x0002, 0, 0, 0, 0)   # LEFTDOWN
Start-Sleep -Milliseconds 100
[Win32Cap2]::SetCursorPos($sx + 200, $sy) | Out-Null
Start-Sleep -Milliseconds 100
[Win32Cap2]::mouse_event(0x0004, 0, 0, 0, 0)   # LEFTUP
Write-Output ("select drag: ({0},{1}) -> ({2},{3})" -f $sx, $sy, ($sx+200), $sy)

Start-Sleep -Milliseconds 1800   # 等取词 + 胶囊弹出

[void][Win32Cap2]::SetProcessDPIAware()   # 本脚本进程默认不感知, 先置感知以捕获物理像素
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($vs.X, $vs.Y, 0, 0, $vs.Size)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved $Out ($($vs.Width)x$($vs.Height))"
