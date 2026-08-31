// 自动拆分自 Program.cs - 模块: FloatingButtonForm
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LLMTranslation
{
    public class FloatingButtonForm : Form
    {
        public event EventHandler TranslateClicked;

        public FloatingButtonForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            // 胶囊固定 100% 设计尺寸 (98×40), 不随系统缩放放大:
            // 实测旧版在本系统上就是 98×40 物理 1:1 + pt 字体按真实 120dpi 渲染的大字;
            // 窗口尺寸钉死, 字体保持 pt 声明按真实 DPI 换算, 与旧版像素级一致。
            Size = new Size(98, 40);
            DoubleBuffered = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;   // 关键色透明, 跨设备稳定(不依赖分层窗口)
            Text = "LLMT_FLOAT";   // 稳定标题, 供自动化测试定位
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void Popup(Point p)
        {
            AppContext.Log("Popup at " + p);
            Rectangle wa = Screen.FromPoint(p).WorkingArea;
            int x = p.X + 2;
            int y = p.Y + 10;
            if (y + Height > wa.Bottom) y = p.Y - Height - 6;   // 下方放不下则翻到上方
            if (x + Width > wa.Right) x = wa.Right - Width - 2;
            // 最终钳制在工作区内, 保证任何多屏/DPI 下都可见
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            if (x + Width > wa.Right) x = wa.Right - Width;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            Location = new Point(x, y);
            AppContext.Log("Popup final bounds=" + Bounds + " scale=" + Dpi.Scale.ToString("0.##") + " shellDpi=" + NativeMethods.GetDpiForSystem());
            if (!Visible) Show();
            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WinLog.Window("capsule", this);   // 纯日志: 胶囊实际尺寸/位置/DPI
        }

        public void HidePopup()
        {
            if (Visible) Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(TransparencyKey);   // 关键色透明
            // 字体保持 pt 声明: 实测旧版(不感知)在本系统上窗口 DC 报真实 120dpi,
            // pt→px 按 120 换算(10.5pt=17.5px)——即用户熟悉的"小胶囊配大字"。
            // 感知进程同样按真实 DPI 换算, 像素级一致; 100% 机器上则回到 14px 原始设计。
            Rectangle r = new Rectangle(0, 0, 97, 39);
            using (GraphicsPath path = RoundRect(r, 9))
            {
                using (SolidBrush brush = new SolidBrush(Color.White)) g.FillPath(brush, path);
                using (Pen pen = new Pen(Color.FromArgb(210, 210, 210))) g.DrawPath(pen, path);
            }
            using (Font f1 = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            using (Font f2 = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
            using (Font f3 = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular))
            using (SolidBrush fg = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                g.DrawString("文", f1, fg, 8, 4);
                g.DrawString("A", f2, fg, 18, 17);
                g.DrawString("翻译", f3, fg, 42, 9);
            }
            base.OnPaint(e);
        }

        internal static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (TranslateClicked != null) TranslateClicked(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = Cursors.Hand;
        }
    }

    // ---------------- 翻译结果小窗 (Cherry Studio 风格) ----------------
}
