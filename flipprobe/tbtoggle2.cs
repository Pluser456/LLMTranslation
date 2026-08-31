// 真实鼠标两次点击任务栏按钮: 期望 恢复 → 再点自动最小化 (Win11 标准切换行为)
using System;
using System.Threading;
using System.Windows.Automation;
using System.Runtime.InteropServices;

class TbToggle2
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);

    static void Click(int x, int y)
    {
        SetCursorPos(x, y); Thread.Sleep(200);
        mouse_event(2, 0, 0, 0, 0); Thread.Sleep(70); mouse_event(4, 0, 0, 0, 0);
        Thread.Sleep(900);
    }

    static void Main()
    {
        SetProcessDPIAware();
        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr == IntPtr.Zero || !IsWindowVisible(tr)) { Console.WriteLine("NO WINDOW"); return; }

        // 定位任务栏按钮 (Name 以 LLMTranslation.exe 开头, 类 Taskbar.TaskListButtonAutomationPeer)
        var tray = AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        var btns = tray.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        AutomationElement btn = null;
        foreach (AutomationElement b in btns)
            if (b.Current.Name.StartsWith("LLMTranslation.exe")) { btn = b; break; }
        if (btn == null) { Console.WriteLine("NO TASKBAR BUTTON"); return; }
        var r = btn.Current.BoundingRectangle;
        int cx = (int)(r.X + r.Width / 2), cy = (int)(r.Y + r.Height / 2);
        Console.WriteLine("button @(" + cx + "," + cy + ") name=[" + btn.Current.Name + "]");

        // 起点: 最小化
        ShowWindow(tr, 7); Thread.Sleep(900);
        Console.WriteLine("minimized:            iconic=" + IsIconic(tr));

        Click(cx, cy);
        Console.WriteLine("after click #1:       iconic=" + IsIconic(tr) + " fg-is-tr=" + (GetForegroundWindow() == tr));

        Click(cx, cy);
        Console.WriteLine("after click #2:       iconic=" + IsIconic(tr) + "  => 二次点击切换 " + (IsIconic(tr) ? "生效" : "未生效"));

        Click(cx, cy);
        Console.WriteLine("after click #3:       iconic=" + IsIconic(tr) + " fg-is-tr=" + (GetForegroundWindow() == tr));

        if (IsIconic(tr)) { ShowWindow(tr, 9); Thread.Sleep(400); }
        Console.WriteLine("done");
    }
}
