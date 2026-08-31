// 无侵入: 读翻译窗 WM_GETICON 的图标句柄, 渲染成 png 目检 (任务栏按钮显示的就是它)
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class IconPeek
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    struct RECT { public int L, T, R, B; }

    static void Main()
    {
        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr == IntPtr.Zero) { Console.WriteLine("NO WINDOW (未打开翻译窗)"); return; }
        RECT rr; GetWindowRect(tr, out rr);
        Console.WriteLine("visible=" + IsWindowVisible(tr) + " rect=(" + rr.L + "," + rr.T + "," + rr.R + "," + rr.B + ")");
        IntPtr small2 = SendMessage(tr, 0x00D9 /*WM_GETICON*/, new IntPtr(0), IntPtr.Zero);
        IntPtr small = SendMessage(tr, 0x00D9, new IntPtr(1), IntPtr.Zero);
        IntPtr big = SendMessage(tr, 0x00D9, new IntPtr(2), IntPtr.Zero);
        IntPtr clsSmall = new IntPtr(GetWindowLong(tr, -14 /*GCL_HICONSM*/));
        IntPtr clsBig = new IntPtr(GetWindowLong(tr, -12 /*GCL_HICON*/));
        Console.WriteLine("WM_GETICON small2=" + small2 + " small=" + small + " big=" + big + " class hicon=" + clsBig + " hiconsm=" + clsSmall);
        IntPtr use = big != IntPtr.Zero ? big : (small2 != IntPtr.Zero ? small2 : (clsBig != IntPtr.Zero ? clsBig : clsSmall));
        if (use == IntPtr.Zero) { Console.WriteLine("NO ICON AT ALL -> 任务栏会显示默认应用图标 (bug 确认)"); return; }
        using (Bitmap bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Pink);
                IntPtr hdc = g.GetHdc();
                DrawIconEx(hdc, 0, 0, use, 64, 64, 0, IntPtr.Zero, 3 /*DI_NORMAL*/);
                g.ReleaseHdc(hdc);
            }
            bmp.Save("D:\\Programs\\LLMTranslation\\flipprobe\\icon_peek.png", ImageFormat.Png);
        }
        Console.WriteLine("saved icon_peek.png (64x64 放大自窗口图标)");
    }
    [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hicon, int w, int ht, uint step, IntPtr brush, uint flags);
}
