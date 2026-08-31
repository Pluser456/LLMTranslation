// 持续监控探针: 捕获进程 DPI 感知状态切换的精确时刻
// 启动状态 = 不感知 (与应用相同), 每 500ms 采样, 仅状态变化时写日志
using System;
using System.IO;
using System.Runtime.InteropServices;

class Probe
{
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    static extern bool EnumDisplayDevicesA(string name, uint idx, ref DEV dev);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DEV
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        public IntPtr DeviceId;
        public IntPtr DeviceKey;
    }

    static string Vgpu()
    {
        try
        {
            for (uint i = 0; i < 8; i++)
            {
                var d = new DEV(); d.cb = Marshal.SizeOf(typeof(DEV));
                if (!EnumDisplayDevicesA(null, i, ref d)) break;
                if (d.DeviceString != null && d.DeviceString.IndexOf("GameViewer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (d.StateFlags & 1) != 0 ? "active" : "inactive";  // 1 = DISPLAY_DEVICE_ACTIVE
            }
        }
        catch { }
        return "-";
    }

    static void Main()
    {
        using (var w = new StreamWriter("flip-probe.log", false))
        {
            uint dpi = GetDpiForSystem();
            w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " probe start dpi=" + dpi + " cx=" + GetSystemMetrics(0) + " vgpu=" + Vgpu());
            w.Flush();
            uint lastDpi = dpi; string lastV = Vgpu(); int lastCx = GetSystemMetrics(0);
            while (true)
            {
                try
                {
                    uint d = GetDpiForSystem();
                    int cx = GetSystemMetrics(0);
                    string v = Vgpu();
                    if (d != lastDpi || cx != lastCx || v != lastV)
                    {
                        w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " CHANGE dpi=" + d + " cx=" + cx + " vgpu=" + v);
                        w.Flush();
                        lastDpi = d; lastCx = cx; lastV = v;
                    }
                }
                catch { }
                System.Threading.Thread.Sleep(500);
            }
        }
    }
}
