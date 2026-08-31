Add-Type -TypeDefinition @'
using System; using System.Text; using System.Runtime.InteropServices;
public class W2 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EW cb, IntPtr l);
  public delegate bool EW(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L,T,R,B; }
  public static string All() { var outp = new StringBuilder();
    EnumWindows((h,l)=>{ var sb=new StringBuilder(256); int n=GetWindowTextW(h,sb,256);
      if (n>0 && IsWindowVisible(h)) { RECT r; GetWindowRect(h,out r);
        outp.Append(sb.ToString()).Append(" @").Append(r.L).Append(",").Append(r.T).Append(" h=").Append(h.ToInt64()).AppendLine(); }
      return true; }, IntPtr.Zero); return outp.ToString(); }
}
'@
$t = [W2]::All()
$t -split "`n" | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { Write-Output $_ }
