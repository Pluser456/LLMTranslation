// 验证: 真实点击任务栏按钮恢复最小化的翻译窗
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

class TbClick
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    struct RECT { public int L, T, R, B; }

    static void Main(string[] argv)
    {
        SetProcessDPIAware();
        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr == IntPtr.Zero) { Console.WriteLine("NO TRANSLATE WINDOW"); return; }
        Console.WriteLine("before click: iconic=" + IsIconic(tr));
        int x = int.Parse(argv[0]), y = int.Parse(argv[1]);
        SetCursorPos(x, y); Thread.Sleep(150);
        mouse_event(2, 0, 0, 0, 0); Thread.Sleep(60); mouse_event(4, 0, 0, 0, 0);
        Thread.Sleep(800);
        RECT r; GetWindowRect(tr, out r);
        Console.WriteLine("after click: iconic=" + IsIconic(tr) + " rect=(" + r.L + "," + r.T + "," + r.R + "," + r.B + ")");
    }
}
