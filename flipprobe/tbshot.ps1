Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class MM {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public static void Click(int x, int y) { SetCursorPos(x,y); System.Threading.Thread.Sleep(150); mouse_event(2,0,0,0,0); System.Threading.Thread.Sleep(60); mouse_event(4,0,0,0,0); }
}
'@
[MM]::SetProcessDPIAware() | Out-Null
# 点 bMin (AutoTest 刚验证过的坐标)
[MM]::Click(1708, 244)
Start-Sleep -m 800
# 截取任务栏中部条带 (Win11 居中图标区)
$b = New-Object System.Drawing.Bitmap(1000, 60)
$g = [System.Drawing.Graphics]::FromImage($b)
$g.CopyFromScreen(460, 1020, 0, 0, (New-Object System.Drawing.Size(1000,60)))
$b.Save("D:\Programs\LLMTranslation\flipprobe\taskbar.png")
Write-Host "SHOT taskbar.png"
