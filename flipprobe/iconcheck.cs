// 完整链路: (必要时拉起 TestTarget) → 拖选 → 点胶囊开翻译窗 → 点 bMin 最小化 → 截任务栏条带
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

class IconCheck
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, int e);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    struct RECT { public int L, T, R, B; }

    static void Down(int x, int y) { SetCursorPos(x, y); Thread.Sleep(120); mouse_event(2, 0, 0, 0, 0); Thread.Sleep(60); }
    static void Up(int x, int y) { SetCursorPos(x, y); Thread.Sleep(60); mouse_event(4, 0, 0, 0, 0); Thread.Sleep(120); }
    static void Click(int x, int y) { Down(x, y); Up(x, y); }

    static IntPtr Wait(string title, int ms)
    {
        for (int i = 0; i < ms / 100; i++) { var h = FindWindow(null, title); if (h != IntPtr.Zero && IsWindowVisible(h)) return h; Thread.Sleep(100); }
        return IntPtr.Zero;
    }

    static void Main()
    {
        SetProcessDPIAware();
        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr != IntPtr.Zero && !IsWindowVisible(tr)) tr = IntPtr.Zero;   // 隐藏常驻窗不算"已打开"
        if (tr != IntPtr.Zero && IsIconic(tr)) ShowWindow(tr, 9);

        if (tr == IntPtr.Zero)
        {
            if (Process.GetProcessesByName("TestTarget").Length == 0)
                Process.Start("D:\\Programs\\LLMTranslation\\dist\\TestTarget.exe");
            IntPtr tgt = Wait("TestTargetApp", 5000);
            if (tgt == IntPtr.Zero) { Console.WriteLine("NO TESTTARGET"); return; }
            MoveWindow(tgt, 1340, 120, 520, 340, true); Thread.Sleep(300);
            SetForegroundWindow(tgt); Thread.Sleep(400);
            RECT t; GetWindowRect(tgt, out t);
            Down(t.L + 60, t.T + 110);
            for (int x = t.L + 60; x <= t.L + 300; x += 24) { SetCursorPos(x, t.T + 110); Thread.Sleep(25); }
            Up(t.L + 300, t.T + 110);
            IntPtr cap = Wait("LLMT_FLOAT", 3000);
            if (cap == IntPtr.Zero) { Console.WriteLine("NO CAPSULE"); return; }
            RECT c; GetWindowRect(cap, out c);
            Click((c.L + c.R) / 2, (c.T + c.B) / 2);
            tr = Wait("LLM 划词翻译", 5000);
            if (tr == IntPtr.Zero) { Console.WriteLine("NO TRANSLATE"); return; }
            Thread.Sleep(1200);
        }

        RECT r; GetWindowRect(tr, out r);
        float s = (r.R - r.L) / 520f;
        Click(r.R - (int)Math.Round(55 * s), r.T + (int)Math.Round(19 * s));   // bMin
        Thread.Sleep(900);
        Console.WriteLine("iconic=" + IsIconic(tr));
        using (Bitmap bmp = new Bitmap(1000, 60))
        {
            using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(460, 1020, 0, 0, new Size(1000, 60));
            bmp.Save("D:\\Programs\\LLMTranslation\\flipprobe\\taskbar2.png", ImageFormat.Png);
        }
        Console.WriteLine("saved taskbar2.png (窗口保持最小化, 请目视任务栏)");
    }
}
