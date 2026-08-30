// 纯窗口状态日志 (排查高 DPI/缩放问题的诊断工具):
// 只往 log.txt 写记录, 不改变任何窗口行为/尺寸/位置。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LLMTranslation
{
    internal static class WinLog
    {
        private sealed class Snap
        {
            public int Tick;
            public Size Size;
        }
        private static readonly Dictionary<Form, Snap> _last = new Dictionary<Form, Snap>();

        // 启动时 DPI 快照: 进程锁定 DPI (device caps) vs 系统当前真实 DPI (Shell API, 不受进程锁定影响)
        // 两者不一致 => 窗口会被 DWM 位图缩放 (模糊/偏大/偏小)
        public static void Startup()
        {
            try
            {
                float scale = Dpi.Scale;
                uint shell;
                try { shell = NativeMethods.GetDpiForSystem(); } catch { shell = 0; }
                int awareness;
                try { awareness = NativeMethods.GetProcessDpiAwarenessContext(0); } catch { awareness = -1; }   // 部分系统无此导出 => -1
                string mons = "";
                foreach (Screen sc in Screen.AllScreens)
                    mons += sc.Bounds.ToString() + (sc.Primary ? "*" : " ") + " ";
                AppContext.Log(string.Format(
                    "DPI: locked={0:0.##}({1}dpi) shellNow={2}({3:0.##}) awareness={4} screen={5} monitors=[{6}]",
                    scale, (int)(scale * 96f + 0.5f), shell, shell / 96f, awareness,
                    Screen.PrimaryScreen.Bounds.ToString(), mons.Trim()));
            }
            catch { }
        }

        // 窗口状态一行快照: 尺寸/位置/字体/自动缩放参数/DPI 锁定值/当前真实 DPI/圆角 Region
        public static void Window(string tag, Form f)
        {
            try
            {
                string region = "";
                try
                {
                    if (f.Region == null) region = "none";
                    else
                    {
                        using (Graphics g = f.CreateGraphics())
                            region = f.Region.GetBounds(g).ToString();
                    }
                }
                catch { region = "?"; }
                string font = f.Font == null ? "?" : (f.Font.Size + "/" + f.Font.Unit + " " + f.Font.Name);
                AppContext.Log(string.Format(
                    "WIN {0}: size={1} client={2} loc={3} font={4} autoMode={5} autoDims=({6:0.##},{7:0.##}) locked={8:0.##} shellNow={9} region={10}",
                    tag, f.Size.ToString(), f.ClientSize.ToString(), f.Location.ToString(),
                    font, f.AutoScaleMode.ToString(),
                    f.AutoScaleDimensions.Width, f.AutoScaleDimensions.Height,
                    Dpi.Scale, NativeMethods.GetDpiForSystem(), region));
            }
            catch { }
        }

        // SizeChanged 节流日志: 尺寸大幅跳变 (>50px, 如 DPI 自动缩放/显示器变更) 立即记, 否则最多 500ms 一条
        public static void SizeChanged(string tag, Form f)
        {
            try
            {
                int now = Environment.TickCount;
                Snap s = null;
                _last.TryGetValue(f, out s);
                bool jump = s != null && (Math.Abs(f.Size.Width - s.Size.Width) > 50 || Math.Abs(f.Size.Height - s.Size.Height) > 50);
                if (!jump && s != null && now - s.Tick < 500) return;
                if (s == null)
                {
                    s = new Snap();
                    _last[f] = s;
                }
                s.Tick = now;
                s.Size = f.Size;
                Window(tag, f);
            }
            catch { }
        }
    }
}
