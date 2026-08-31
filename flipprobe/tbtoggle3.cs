// 对照实验: 任务栏二次点击切换失效的根因
//   mode=notop    : 把翻译窗设为非置顶后测试
//   mode=notepad  : 记事本对照组
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using System.Runtime.InteropServices;

class TbToggle3
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    const uint SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOACTIVATE = 0x10;

    static void Click(int x, int y) { SetCursorPos(x, y); Thread.Sleep(200); mouse_event(2, 0, 0, 0, 0); Thread.Sleep(70); mouse_event(4, 0, 0, 0, 0); Thread.Sleep(900); }

    static AutomationElement FindBtn(string namePrefix)
    {
        var tray = AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        var btns = tray.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement b in btns) if (b.Current.Name.StartsWith(namePrefix)) return b;
        return null;
    }

    static void Main(string[] argv)
    {
        SetProcessDPIAware();
        string mode = argv.Length > 0 ? argv[0] : "notop";
        IntPtr tr = IntPtr.Zero; string btnName;

        if (mode == "notepad")
        {
            if (Process.GetProcessesByName("notepad").Length == 0) Process.Start("notepad.exe");
            for (int i = 0; i < 30; i++) { tr = FindWindow("Notepad", null); if (tr != IntPtr.Zero) break; Thread.Sleep(100); }
            btnName = "无标题";
        }
        else
        {
            tr = FindWindow(null, "LLM 划词翻译");
            if (tr == IntPtr.Zero || !IsWindowVisible(tr)) { Console.WriteLine("NO WINDOW - 先打开翻译窗"); return; }
            SetWindowPos(tr, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);  // 关键: 取消置顶
            btnName = "LLMTranslation.exe";
        }
        if (tr == IntPtr.Zero) { Console.WriteLine("NO TARGET"); return; }

        AutomationElement btn = FindBtn(btnName);
        if (btn == null) { Console.WriteLine("NO TASKBAR BUTTON [" + btnName + "]"); return; }
        var r = btn.Current.BoundingRectangle;
        int cx = (int)(r.X + r.Width / 2), cy = (int)(r.Y + r.Height / 2);

        ShowWindow(tr, 7); Thread.Sleep(900);
        Console.WriteLine("[" + mode + "] minimized:      iconic=" + IsIconic(tr));
        Click(cx, cy);
        Console.WriteLine("[" + mode + "] click#1 (恢复): iconic=" + IsIconic(tr));
        Click(cx, cy);
        Console.WriteLine("[" + mode + "] click#2 (切换?): iconic=" + IsIconic(tr) + (IsIconic(tr) ? "  => 切换生效" : "  => 切换未生效"));
        if (IsIconic(tr)) { ShowWindow(tr, 9); Thread.Sleep(300); }
    }
}
