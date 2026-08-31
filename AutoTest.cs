// 自动化测试: 真实鼠标划词 -> 验证胶囊出现且真正绘制; 并测翻译窗开启后复划
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class AutoTest
{
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    static void ForceForeground(IntPtr h) { keybd_event(0x12, 0, 0, UIntPtr.Zero); SetForegroundWindow(h); keybd_event(0x12, 0, 2, UIntPtr.Zero); }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    const uint MOUSEEVENTF_MOVE = 1, LEFTDOWN = 2, LEFTUP = 4, ABSOLUTE = 0x8000;

    static void Mouse(uint flags, int x, int y)
    {
        var vs = SystemInformation.VirtualScreen;
        INPUT i = new INPUT(); i.type = 0;
        i.mi.dx = (int)(((long)(x - vs.X) * 65535) / (vs.Width - 1));
        i.mi.dy = (int)(((long)(y - vs.Y) * 65535) / (vs.Height - 1));
        i.mi.dwFlags = flags | ABSOLUTE;
        SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
    }
    static void Move(int x, int y) { Mouse(MOUSEEVENTF_MOVE, x, y); }
    static void Down(int x, int y) { Move(x, y); Thread.Sleep(30); Mouse(LEFTDOWN, x, y); }
    static void Up(int x, int y) { Mouse(LEFTUP, x, y); }
    // 泵消息的睡眠: 本进程可能是剪贴板 owner, 纯 Sleep 会让其他进程 OpenClipboard 等 4-5s 超时
    static void PumpSleep(int ms)
    {
        int start = Environment.TickCount;
        while (unchecked(Environment.TickCount - start) < ms) { Application.DoEvents(); Thread.Sleep(10); }
    }

    static Color Pixel(int x, int y)
    {
        using (var b = new Bitmap(1, 1))
        { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(x, y, 0, 0, new Size(1, 1)); return b.GetPixel(0, 0); }
    }
    static bool IsWhite(Color c) { return c.R > 200 && c.G > 200 && c.B > 200; }

    static IntPtr WaitCapsule(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            IntPtr h = FindWindow(null, "LLMT_FLOAT");
            if (h != IntPtr.Zero && IsWindowVisible(h)) return h;
            Thread.Sleep(50);
        }
        return IntPtr.Zero;
    }

    // 返回: 0=未出现 1=出现且绘制 2=出现但空白
    static int CheckCapsule(out string detail)
    {
        IntPtr h = WaitCapsule(1200);
        if (h == IntPtr.Zero) { detail = "capsule not found"; return 0; }
        Thread.Sleep(300); // 等绘制
        RECT r; GetWindowRect(h, out r);
        int cx = (r.Left + r.Right) / 2, cy = (r.Top + r.Bottom) / 2;
        // 探针点: 胶囊宽 1/3 处 ("A"与"翻译"之间的留白列), 避开字形笔画;
        // 旧版中心点探针在 17.5px 字号下正好落在"翻"字笔画上导致误报 BLANK
        int px_ = r.Left + (r.Right - r.Left) / 3;
        Color c = Pixel(px_, cy);
        detail = string.Format("rect=({0},{1},{2},{3}) probe=({4},{5}) px={6}", r.Left, r.Top, r.Right, r.Bottom, px_, cy, c);
        return IsWhite(c) ? 1 : 2;
    }

    static void DragSelect(RECT t, int y, int x2)
    {
        int x1 = t.Left + 60;
        Down(x1, y);
        for (int x = x1; x <= x2; x += 20) { Move(x, y); Thread.Sleep(10); }
        Up(x2, y);
    }

    static void ResetClick(RECT t)
    {
        int x = t.Left + 30, y = t.Top + 200;
        Down(x, y); Up(x, y);
        Thread.Sleep(150);
    }

    [STAThread]
    static int Main()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        IntPtr tgt = FindWindow(null, "TestTargetApp");
        if (tgt == IntPtr.Zero) { Process.Start(baseDir + "\\dist\\TestTarget.exe"); Thread.Sleep(800); tgt = FindWindow(null, "TestTargetApp"); }
        if (tgt == IntPtr.Zero) { Console.WriteLine("NO TARGET"); return 1; }
        ShowWindow(tgt, 9);
        var vs0 = SystemInformation.VirtualScreen;
        MoveWindow(tgt, vs0.Right - 580, 120, 520, 340, true);   // 放右侧, 避开本终端窗口
        ForceForeground(tgt); Thread.Sleep(300);

        int pass = 0, blank = 0, missing = 0;
        for (int i = 1; i <= 8; i++)
        {
            ForceForeground(tgt); Thread.Sleep(200);
            RECT t; GetWindowRect(tgt, out t);
            ResetClick(t);
            DragSelect(t, t.Top + 60, t.Left + 320);
            string d; int r = CheckCapsule(out d);
            if (r == 1) pass++; else if (r == 2) blank++; else missing++;
            Console.WriteLine("iter " + i + " => " + (r == 1 ? "PASS" : r == 2 ? "BLANK" : "MISSING") + " | " + d);
            // 关掉胶囊
            ResetClick(t);
        }
        Console.WriteLine("SUMMARY basic: pass=" + pass + " blank=" + blank + " missing=" + missing);

        // 翻译窗开启后复划
        ForceForeground(tgt); Thread.Sleep(300);
        RECT tt; GetWindowRect(tgt, out tt);
        ResetClick(tt); Thread.Sleep(300);
        DragSelect(tt, tt.Top + 110, tt.Left + 280);
        IntPtr cap = WaitCapsule(2000);
        if (cap != IntPtr.Zero)
        {
            RECT cr; GetWindowRect(cap, out cr);
            Down((cr.Left + cr.Right) / 2, (cr.Top + cr.Bottom) / 2); Up((cr.Left + cr.Right) / 2, (cr.Top + cr.Bottom) / 2);
            Thread.Sleep(1500); // 等翻译窗
            ForceForeground(tgt); Thread.Sleep(300);
            GetWindowRect(tgt, out tt);
            // 复划必须划在翻译窗未遮挡的区域: 用测试窗顶部条带, 避开下方已打开的翻译窗
            int yResel = tt.Top + 30;
            IntPtr trNow = FindWindow(null, "LLM 划词翻译");
            if (trNow != IntPtr.Zero) { RECT trr0; GetWindowRect(trNow, out trr0); if (yResel >= trr0.Top) yResel = tt.Top + 10; }
            DragSelect(tt, yResel, tt.Left + 300);
            string d2; int r2 = CheckCapsule(out d2);
            Console.WriteLine("translate-open reselect => " + (r2 == 1 ? "PASS" : r2 == 2 ? "BLANK" : "MISSING") + " | " + d2);
        }
        else Console.WriteLine("translate-open reselect => no initial capsule");

        // 拖动翻译窗标题栏(>8px): 不应弹出胶囊 (原生 caption 拖动不经过 DragActive 标志)
        IntPtr tr = FindWindow(null, "LLM 划词翻译");
        if (tr != IntPtr.Zero && IsWindowVisible(tr))
        {
            // 先在测试窗内点一下, 确保旧胶囊已隐藏
            RECT t2; GetWindowRect(FindWindow(null, "TestTargetApp"), out t2);
            ResetClick(t2); Thread.Sleep(300);
            RECT trr; GetWindowRect(tr, out trr);
            int ty = trr.Top + 15, tx1 = trr.Left + 80, tx2 = trr.Left + 200;
            Down(tx1, ty);
            for (int x = tx1; x <= tx2; x += 20) { Move(x, ty); Thread.Sleep(10); }
            Up(tx2, ty);
            Thread.Sleep(800);   // 若 bug 存在, 胶囊会在 ~200ms 后弹出
            IntPtr cap2 = FindWindow(null, "LLMT_FLOAT");
            bool vis = cap2 != IntPtr.Zero && IsWindowVisible(cap2);
            Console.WriteLine("titlebar drag no-capsule => " + (vis ? "FAIL (capsule appeared)" : "PASS"));
        }
        else Console.WriteLine("titlebar drag no-capsule => SKIPPED (no translate window)");

        // 最小化到任务栏: 点标题栏 bMin → 真最小化 (窗口存活且 IsIconic); ShowWindow 恢复后位置尺寸不变。
        // bMin 坐标按 CSS 几何: 右内边距8 + close宽30 + gap2 + 半宽15 = 右缩55, y=标题栏38/2=19; 物理缩放用窗口宽/设计宽520自校准
        IntPtr trm = FindWindow(null, "LLM 划词翻译");
        if (trm != IntPtr.Zero && IsWindowVisible(trm))
        {
            RECT mr; GetWindowRect(trm, out mr);
            // 窗口可能被用户拖到屏外 → 先拉回屏内安全区再测 (尺寸不变)
            int mw = mr.Right - mr.Left, mh = mr.Bottom - mr.Top;
            if (mr.Right > 1900 || mr.Left < 20 || mr.Bottom > 1060) { MoveWindow(trm, 900, 220, mw, mh, true); Thread.Sleep(300); GetWindowRect(trm, out mr); }
            float ms = (mr.Right - mr.Left) / 520f;
            int bx = mr.Right - (int)Math.Round(55 * ms), by = mr.Top + (int)Math.Round(19 * ms);
            Down(bx, by); Up(bx, by);
            PumpSleep(700);
            bool iconic = IsIconic(trm), alive = IsWindow(trm) && IsWindowVisible(trm);
            Console.WriteLine("minimize-to-taskbar => " + (iconic && alive ? "PASS" : "FAIL") + " | iconic=" + iconic + " alive=" + alive + " click=(" + bx + "," + by + ") scale=" + ms.ToString("0.00"));
            if (alive && iconic)
            {
                ShowWindow(trm, 9); // SW_RESTORE: 等价于点任务栏按钮
                PumpSleep(500);
                RECT ar; GetWindowRect(trm, out ar);
                bool same = Math.Abs(ar.Left - mr.Left) < 4 && Math.Abs(ar.Top - mr.Top) < 4
                    && Math.Abs((ar.Right - ar.Left) - (mr.Right - mr.Left)) < 4 && !IsIconic(trm);
                Console.WriteLine("restore-from-taskbar => " + (same ? "PASS" : "FAIL") + " | before=(" + mr.Left + "," + mr.Top + "," + (mr.Right - mr.Left) + "x" + (mr.Bottom - mr.Top) + ") after=(" + ar.Left + "," + ar.Top + "," + (ar.Right - ar.Left) + "x" + (ar.Bottom - ar.Top) + ")");
            }
            else if (alive) Console.WriteLine("restore-from-taskbar => FAIL (未最小化, 跳过恢复验证)");
            else Console.WriteLine("restore-from-taskbar => FAIL (window gone)");
        }
        else Console.WriteLine("minimize-to-taskbar => SKIPPED (no translate window)");

        // 复制验证: 翻译窗打开时按 C, 剪贴板必须被写入译文 (用户关心的"复制失效"场景)。
        // 先用哨兵值覆盖剪贴板, 按 C 后必须不再是哨兵 (与翻译内容是否恰好相同无关)
        IntPtr tr3 = FindWindow(null, "LLM 划词翻译");
        if (tr3 != IntPtr.Zero && IsWindowVisible(tr3))
        {
            const string sentinel = "LLMT_SENTINEL_9f3a2b";
            try { Clipboard.SetText(sentinel); Console.WriteLine("copy: sentinel set ok"); }
            catch (Exception e) { Console.WriteLine("copy: sentinel set EX " + e.Message); }
            string rb = null; try { if (Clipboard.ContainsText()) rb = Clipboard.GetText(); } catch { }
            Console.WriteLine("copy: sentinel readback=" + (rb == null ? "null" : (rb == sentinel ? "OK" : "MISMATCH(" + rb.Length + ")")));
            ForceForeground(tr3); Thread.Sleep(300);
            Console.WriteLine("copy: pressing C at " + DateTime.Now.ToString("HH:mm:ss.fff"));
            keybd_event(0x43, 0, 0, UIntPtr.Zero); keybd_event(0x43, 0, 2, UIntPtr.Zero);
            // DoCopy 约 300ms 内执行; 轮询读取最多 4s (泵消息, 避免本进程作为剪贴板 owner 卡死对方)。
            // 成功判据: 非空且非哨兵 (空串可能是他处 Ctrl+C 空选区, 需继续等真正的译文写入)
            string afterClip = null; bool got = false;
            for (int i = 0; i < 20 && !got; i++)
            {
                PumpSleep(200);
                try { if (Clipboard.ContainsText()) { afterClip = Clipboard.GetText(); if (afterClip != null && afterClip != sentinel && afterClip.Length > 0) { got = true; Console.WriteLine("copy: read at +" + ((i + 1) * 200) + "ms len=" + afterClip.Length); } else { Console.WriteLine("copy: read at +" + ((i + 1) * 200) + "ms len=0 or sentinel, keep waiting"); } } } catch (Exception e) { Console.WriteLine("copy: read EX " + e.Message); }
            }
            bool ok = got && !string.IsNullOrEmpty(afterClip);
            Console.WriteLine("copy-to-clipboard => " + (ok ? "PASS" : "FAIL") + " | after=" + (afterClip == null ? "null" : afterClip.Length.ToString()));
        }
        else Console.WriteLine("copy-to-clipboard => SKIPPED (no translate window)");

        return 0;
    }
}
