// 隔离实验 v3: 测试序列跑在后台线程, UI 线程自由泵消息 (与应用真实运行方式一致)
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HookProbe
{
    static class P
    {
        static void Log(string s) { Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s); try { Console.Out.Flush(); } catch { } }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSE mi; }
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSE { public int dx, dy; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] a, int sz);
        const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004, ABS = 0x8000, MOVE = 0x0001;

        static void MoveAbs(int x, int y)
        {
            var i = new INPUT { type = 0 };
            i.mi.dx = (x * 65535) / 1920;
            i.mi.dy = (y * 65535) / 1080;
            i.mi.flags = ABS | MOVE;
            SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
        }
        static void Btn(uint flag)
        {
            var i = new INPUT { type = 0 };
            i.mi.flags = flag;
            SendInput(1, new INPUT[] { i }, Marshal.SizeOf(typeof(INPUT)));
        }
        static void Click(int x, int y) { MoveAbs(x, y); Btn(LEFTDOWN); Btn(LEFTUP); }
        static void Sleep(int ms) { System.Threading.Thread.Sleep(ms); }

        delegate IntPtr HookProc(int n, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr hMod, int tid);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string s);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
        [StructLayout(LayoutKind.Sequential)] struct PT { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out PT p);

        static HookProc _d;
        static IntPtr _hook;
        static System.Collections.Generic.List<object> _keep = new System.Collections.Generic.List<object>();
        static int cbTotal = 0, cbLBDN = 0, cbLBUP = 0, cbMove = 0;
        static Form _form;

        static IntPtr H(int n, IntPtr w, IntPtr l)
        {
            if (n < 0) return CallNextHookEx(_hook, n, w, l);
            cbTotal++;
            long m = (long)w;
            if (m == 0x0201) cbLBDN++;
            else if (m == 0x0202) cbLBUP++;
            else if (m == 0x0200) cbMove++;
            return CallNextHookEx(_hook, n, w, l);
        }

        static string FgTitle()
        {
            IntPtr fg = GetForegroundWindow();
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(fg, sb, 256);
            return sb.ToString();
        }

        static void Main()
        {
            Application.EnableVisualStyles();
            _form = new Form();
            _form.Text = "HOOKPROBE_FORM";
            _form.StartPosition = FormStartPosition.Manual;
            _form.Location = new Point(400, 300);
            _form.Size = new Size(300, 200);
            _form.ShowInTaskbar = true;
            _form.Show();

            _d = new HookProc(H);
            _keep.Add(_d);
            _hook = SetWindowsHookEx(14, _d, GetModuleHandle(null), 0);
            Log("hook installed ptr=0x" + ((long)_hook).ToString("x"));

            int tick = 0;
            var tm = new System.Windows.Forms.Timer { Interval = 1000 };
            tm.Tick += delegate
            {
                tick++;
                var c = new PT();
                GetCursorPos(out c);
                Log(string.Format("tick={0} cb total={1} LBDN={2} LBUP={3} move={4} cursor=({5},{6}) formPos={7} fg=[{8}]",
                    tick, cbTotal, cbLBDN, cbLBUP, cbMove, c.X, c.Y, _form.Location, FgTitle()));
            };
            tm.Start();

            var seq = new System.Threading.Thread(new System.Threading.ThreadStart(delegate
            {
                Sleep(1000);
                Point p = new Point(550, 380); // 表单客户区内
                Point q = new Point(760, 380);  // 表单外 (桌面)

                Log("=== Phase A: baseline 5 次注入点击 — 钩子必须看到 5 LBDN/5 LBUP ===");
                for (int i = 0; i < 5; i++) { Click(q.X, q.Y); Sleep(150); }
                Sleep(700);
                Log("A 结束: cb total=" + cbTotal + " LBDN=" + cbLBDN + " LBUP=" + cbLBUP);

                Log("=== Phase B1: 按住中发合成NC (按钮仍按着), 移动后释放 — 正常拖拽情形 ===");
                MoveAbs(p.X, p.Y); Btn(LEFTDOWN); Sleep(200);
                _form.BeginInvoke(new Action(delegate
                {
                    Log("B1: SendMessage NC 开始 (UI线程)");
                    ReleaseCapture();
                    SendMessage(_form.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                    Log("B1: SendMessage NC 返回 (UI线程)");
                }));
                Sleep(900); // 等待 UI 线程处理 NC 消息 (模态拖拽循环)
                for (int i = 0; i < 6; i++) { MoveAbs(p.X + i * 10, p.Y); Sleep(50); }
                Btn(LEFTUP);
                Sleep(1500);
                Log("B1 后 formPos=" + _form.Location + " cb total=" + cbTotal + " LBDN=" + cbLBDN);

                Log("=== Phase C1: B1 后 3 次点击 — 钩子应继续工作 ===");
                for (int i = 0; i < 3; i++) { Click(q.X, q.Y); Sleep(200); }
                Sleep(700);
                Log("C1 结束: cb total=" + cbTotal + " LBDN=" + cbLBDN + " LBUP=" + cbLBUP);

                Log("=== Phase B2: 快击(50ms 内松开) 200ms 后再发合成NC (按钮已松开 — 悬挂竞态) ===");
                MoveAbs(p.X, p.Y); Btn(LEFTDOWN); Sleep(50); Btn(LEFTUP); Sleep(300);
                _form.BeginInvoke(new Action(delegate
                {
                    Log("B2: SendMessage NC 开始 (UI线程)");
                    ReleaseCapture();
                    SendMessage(_form.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                    Log("B2: SendMessage NC 返回 (UI线程)");
                }));
                Sleep(1800);
                Log("B2 后 formPos=" + _form.Location + " cb total=" + cbTotal);

                Log("=== Phase C2: B2 后 4 次点击 — formPos 跳动=卡模态; LBDN 不增长=钩子失聪 ===");
                for (int i = 0; i < 4; i++) { Click(q.X, q.Y); Sleep(300); }
                Sleep(900);
                Log("C2 后 formPos=" + _form.Location + " cb total=" + cbTotal + " LBDN=" + cbLBDN);

                Log("=== Phase C3: 再 2 次点击 ===");
                for (int i = 0; i < 2; i++) { Click(q.X, q.Y); Sleep(300); }
                Sleep(800);

                Log("=== DONE cb total=" + cbTotal + " LBDN=" + cbLBDN + " LBUP=" + cbLBUP + " formPos=" + _form.Location);
                _form.BeginInvoke(new Action(delegate { _form.Close(); }));
            }));
            seq.IsBackground = true;
            seq.Start();

            Application.Run();
            Log("exited");
        }
    }
}
