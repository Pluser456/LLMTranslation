// 对比 翻译窗 vs 记事本 的窗口样式, 并直接 PostMessage SC_MINIMIZE 测试响应
using System;
using System.Threading;
using System.Runtime.InteropServices;

class StyleCheck
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    static void Report(string tag, IntPtr h)
    {
        if (h == IntPtr.Zero) { Console.WriteLine("[" + tag + "] NO WINDOW"); return; }
        int st = GetWindowLong(h, -16), ex = GetWindowLong(h, -20);
        Console.WriteLine("[" + tag + "] style=0x" + st.ToString("x8") + " ex=0x" + ex.ToString("x8")
            + " CAPTION=" + ((st & 0x00C00000) != 0) + " MINBOX=" + ((st & 0x00020000) != 0)
            + " SYSMENU=" + ((st & 0x00080000) != 0) + " MINIMIZED=" + ((st & 0x20000000) != 0));
        // 直接发 SC_MINIMIZE
        PostMessage(h, 0x0112, new IntPtr(0xF020), IntPtr.Zero);
        Thread.Sleep(700);
        Console.WriteLine("[" + tag + "] after SC_MINIMIZE: iconic=" + IsIconic(h));
        if (IsIconic(h)) { ShowWindow(h, 9); Thread.Sleep(300); }
    }

    static void Main()
    {
        SetProcessDPIAware();
        Report("translate", FindWindow(null, "LLM 划词翻译"));
        Report("notepad", FindWindow("Notepad", null));
    }
}
