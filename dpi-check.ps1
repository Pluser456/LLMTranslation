Add-Type @'
using System;
using System.Runtime.InteropServices;
public class DpiQ {
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
  [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool EnumDisplaySettingsA(string devname, int modeEnum, ref DEV dm);
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
  public struct DEV {
    public int dmSize; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string dmDeviceName;
    public short dmOrientation; public short dmPaperSize; public short dmPaperLength; public short dmPaperWidth;
    public short dmScale; public short dmCopies; public short dmDefaultSource; public short dmPrintQuality;
    public short dmColor; public int dmDuplex; public int dmYResolution; public int dmTTONly; public int dmICCProfile;
    public int dmICMIntent; public int dmAlignment; public int dmStatus; public int dmCopiesSupported;
    public int dmSelection; public int dmDocumentLength; public int dmDocumentWidth; public int dmOrientation2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst=4)] public int[] dmFields;
  }
}
'@
$dev = New-Object DpiQ+DEV
$dev.dmSize = [Runtime.InteropServices.Marshal]::SizeOf($dev)
[void][DpiQ]::EnumDisplaySettingsA($null, -1, [ref]$dev)   # ENUM_CURRENT_SETTINGS
$dmScale = $dev.dmScale
Write-Output ("EnumDisplaySettings dmScale=" + $dmScale + " dmYResolution=" + $dev.dmYResolution)
$dc = [DpiQ]::GetDC([IntPtr]::Zero)
Write-Output ("desktop LOGPIXELSX(88)=" + [DpiQ]::GetDeviceCaps($dc, 88))
$r = Get-CimInstance Win32_VideoController | Select-Object Name, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate
$r | Format-List
try { Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name LogPixels | Select-Object LogPixels } catch {}
