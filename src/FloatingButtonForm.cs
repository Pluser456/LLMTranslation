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
            float s = Dpi.Scale;
            Size = new Size((int)Math.Round(98 * s), (int)Math.Round(40 * s));
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
            // DPI 感知进程: POINT 字体自动按 120dpi 放大(物理尺寸=原位图时代), 布局常量手动 × Dpi.Scale;
            // 不再叠加 ScaleTransform (会与字体自动放大双重缩放)
            float s = Dpi.Scale;
            Rectangle r = new Rectangle(0, 0, (int)Math.Round(97 * s), (int)Math.Round(39 * s));
            using (GraphicsPath path = RoundRect(r, (int)Math.Round(9 * s)))
            {
                using (SolidBrush brush = new SolidBrush(Color.White)) g.FillPath(brush, path);
                using (Pen pen = new Pen(Color.FromArgb(210, 210, 210), (float)(1 * s))) g.DrawPath(pen, path);
            }
            using (Font f1 = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            using (Font f2 = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
            using (Font f3 = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular))
            using (SolidBrush fg = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                g.DrawString("文", f1, fg, (int)(8 * s), (int)(4 * s));
                g.DrawString("A", f2, fg, (int)(18 * s), (int)(17 * s));
                g.DrawString("翻译", f3, fg, (int)(42 * s), (int)(9 * s));
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
