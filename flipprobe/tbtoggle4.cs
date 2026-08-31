// v2 对照: 用应用自身"置顶"按钮两次把 TopMost 属性关掉, 再测任务栏切换
// notepad 组: 轮询等按钮出现, Contains 匹配
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using System.Runtime.InteropServices;

class TbToggle4
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    struct RECT { public int L, T, R, B; }

    static void Click(int x, int y) { SetCursorPos(x, y); Thread.Sleep(200); mouse_event(2, 0, 0, 0, 0); Thread.Sleep(70); mouse_event(4, 0, 0, 0, 0); Thread.Sleep(900); }

    static AutomationElement FindBtn(string sub, int waitMs)
    {
        for (int t = 0; t < waitMs / 500; t++)
        {
            var tray = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
            if (tray != null)
            {
                var btns = tray.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                foreach (AutomationElement b in btns) if (b.Current.Name.Contains(sub)) return b;
            }
            Thread.Sleep(500);
        }
        return null;
    }

    static void Run(string tag, IntPtr tr, AutomationElement btn, bool restoreCheck)
    {
        var r = btn.Current.BoundingRectangle;
        int cx = (int)(r.X + r.Width / 2), cy = (int)(r.Y + r.Height / 2);
        Console.WriteLine("[" + tag + "] button @(" + cx + "," + cy + ") [" + btn.Current.Name + "]");
        ShowWindow(tr, 7); Thread.Sleep(900);
        Console.WriteLine("[" + tag + "] minimized:  iconic=" + IsIconic(tr));
        Click(cx, cy);
        Console.WriteLine("[" + tag + "] click#1:    iconic=" + IsIconic(tr) + " fg=" + (GetForegroundWindow() == tr));
        Click(cx, cy);
        Console.WriteLine("[" + tag + "] click#2:    iconic=" + IsIconic(tr) + (IsIconic(tr) ? "  => 切换生效" : "  => 切换未生效"));
        if (IsIconic(tr)) { ShowWindow(tr, 9); Thread.Sleep(300); }
    }

    static void Main(string[] argv)
    {
        SetProcessDPIAware();
        string mode = argv.Length > 0 ? argv[0] : "notop";

        if (mode == "notepad")
        {
            bool started = Process.GetProcessesByName("notepad").Length == 0;
            if (started) Process.Start("notepad.exe");
            IntPtr np = IntPtr.Zero;
            for (int i = 0; i < 40; i++) { np = FindWindow("Notepad", null); if (np != IntPtr.Zero) break; Thread.Sleep(100); }
            if (np == IntPtr.Zero) { Console.WriteLine("NO NOTEPAD"); return; }
            var btn = FindBtn("记事本", 5000) ?? FindBtn("Notepad", 3000);
            if (btn == null) { Console.WriteLine("NO BUTTON"); if (started) Process.GetProcessesByName("notepad")[0].Kill(); return; }
            Run("notepad", np, btn, false);
            if (started) { ShowWindow(np, 5); }   // 只关自己开的
            return;
        }

        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr == IntPtr.Zero || !IsWindowVisible(tr)) { Console.WriteLine("NO WINDOW - 先打开翻译窗"); return; }
        RECT w; GetWindowRect(tr, out w);
        float s = (w.R - w.L) / 520f;
        int pinx = w.R - (int)Math.Round(87 * s), y = w.T + (int)Math.Round(19 * s);
        if (mode == "notop")
        {
            Click(pinx, y);            // pin on  (TopMost=true)
            Click(pinx, y);            // pin off (TopMost=false, 属性级)
            Console.WriteLine("pin toggled twice -> TopMost=false");
        }
        var b2 = FindBtn("LLMTranslation", 3000);
        if (b2 == null) { Console.WriteLine("NO TASKBAR BUTTON"); return; }
        Run(mode, tr, b2, true);
    }
}
