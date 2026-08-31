using System;
using System.Runtime.InteropServices;

class Api
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr h, string n);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string n);

    static void Try(string name)
    {
        IntPtr p = GetProcAddress(GetModuleHandleA("user32.dll"), name);
        Console.WriteLine(name + " = " + (p == IntPtr.Zero ? "MISSING" : "present"));
    }

    static void Main()
    {
        Try("SetProcessDPIAware");
        Try("SetProcessDpiAwareness");
        Try("SetProcessDpiAwarenessContext");
        Try("GetDpiForSystem");
        Try("GetDpiForWindow");
        IntPtr dm = GetProcAddress(GetModuleHandleA("dwmapi.dll"), "GetDpiForMonitor"); Console.WriteLine("dwmapi.GetDpiForMonitor = " + (dm == IntPtr.Zero ? "MISSING" : "present"));
        // DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = (-1), PER_MONITOR_AWARE_V2 = (-4)
        bool r1 = SetProcessDpiAwarenessContext(new IntPtr(-1));
        Console.WriteLine("SetProcessDpiAwarenessContext(SYSTEM_AWARE) = " + r1 + " GLE=" + Marshal.GetLastWin32Error());
    }
}
