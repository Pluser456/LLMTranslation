// 实测任务栏按钮二次点击切换: 最小化 → UIA Invoke(恢复) → 再 Invoke → 看是否自动最小化
// 用法: tbtoggle.exe [open]   open=先走 划词→点胶囊 链路把翻译窗打开
using System;
using System.Threading;
using System.Windows.Automation;
using System.Drawing;
using System.Runtime.InteropServices;

class TbToggle
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
    struct RECT { public int L, T, R, B; }

    static IntPtr WaitVis(string title, int ms)
    {
        for (int i = 0; i < ms / 100; i++) { var h = FindWindow(null, title); if (h != IntPtr.Zero && IsWindowVisible(h)) return h; Thread.Sleep(100); }
        return IntPtr.Zero;
    }

    static AutomationElement FindTaskbarButton(string name)
    {
        // 先定位任务栏窗口 Shell_TrayWnd, 再在其后代中找同名按钮 (直接按 Name 从根找会匹到窗口元素本身)
        var tray = AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        if (tray == null) return null;
        var and = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, name));
        var all = tray.FindAll(TreeScope.Descendants, and);
        return all.Count > 0 ? all[0] : null;
    }

    static void Main(string[] argv)
    {
        SetProcessDPIAware();
        IntPtr tr = WaitVis("LLM 划词翻译", 2000);
        if (tr == IntPtr.Zero) { Console.WriteLine("NO VISIBLE TRANSLATE WINDOW (先打开翻译窗再跑)"); return; }

        // 1) 最小化
        ShowWindow(tr, 7); // SW_SHOWMINIMIZED
        Thread.Sleep(900);
        Console.WriteLine("step1 minimize: iconic=" + IsIconic(tr));

        // 2) 找任务栏按钮
        AutomationElement btn = FindTaskbarButton("LLM 划词翻译");
        if (btn == null) { Console.WriteLine("step2 UIA: 任务栏按钮未找到"); return; }
        System.Windows.Rect b = btn.Current.BoundingRectangle;
        Console.WriteLine("step2 UIA button found @(" + (int)b.X + "," + (int)b.Y + "," + (int)b.Width + "x" + (int)b.Height + ")");

        // 3) Invoke #1 → 期望恢复
        var inv = (InvokePattern)btn.GetCurrentPattern(InvokePattern.Pattern);
        inv.Invoke();
        Thread.Sleep(900);
        Console.WriteLine("step3 invoke#1: iconic=" + IsIconic(tr) + " foreground-is-tr=" + (GetForegroundWindow() == tr));

        // 4) Invoke #2 → 期望自动最小化 (标准切换行为)
        inv = (InvokePattern)btn.GetCurrentPattern(InvokePattern.Pattern);
        inv.Invoke();
        Thread.Sleep(900);
        bool toggled = IsIconic(tr);
        Console.WriteLine("step4 invoke#2: iconic=" + toggled + "  => 二次点击切换 " + (toggled ? "已生效" : "未生效"));

        // 5) 若 Invoke 未生效, 用真实鼠标点击再试 (行为可能不同)
        if (!toggled && !IsIconic(tr))
        {
            int cx = (int)(b.X + b.Width / 2), cy = (int)(b.Y + b.Height / 2);
            SetCursorPos(cx, cy); Thread.Sleep(150);
            mouse_event(2, 0, 0, 0, 0); Thread.Sleep(60); mouse_event(4, 0, 0, 0, 0);
            Thread.Sleep(900);
            Console.WriteLine("step5 real-click: iconic=" + IsIconic(tr) + " click=(" + cx + "," + cy + ")");
        }

        // 收尾: 恢复窗口
        if (IsIconic(tr)) { ShowWindow(tr, 9); Thread.Sleep(400); }
        Console.WriteLine("done (窗口已恢复)");
    }
}
