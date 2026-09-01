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
            // 胶囊固定 100% 设计尺寸 (98×40), 不随系统缩放放大;
            // 字体同样钉死固定像素字号(见 OnPaint) → 任何缩放屏幕上观感完全一致。
            Size = new Size(98, 40);
            DoubleBuffered = true;
            BackColor = Color.White;
            // 圆角靠窗口 Region 裁剪 (原生 GDI, 无关键色/无分层窗口 → 无品红晕边);
            // 描边为灰色 2px 线 (见 OnPaint)。
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

        // 字体钉死为固定物理像素: 17.5/14.17/18.33px = 旧版(建分支前)在本机的观感
        // (旧版不感知进程的窗口 DC 报真实 120dpi, 10.5/8.5/11pt 按 120 换算而来)。
        // GraphicsUnit.Pixel 为字面设备像素, 不受进程 DPI 影响 → 任何缩放屏幕观感一致。
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.White);
            // 圆角由窗口 Region 裁剪; 描边内缩 1px 避开裁剪边缘, 灰色 2px 线
            Rectangle r = new Rectangle(1, 1, 95, 37);
            using (GraphicsPath path = RoundRect(r, 8))
            {
                using (Pen pen = new Pen(Color.FromArgb(150, 150, 150), 2f)) g.DrawPath(pen, path);
            }
            using (Font f1 = new Font("Microsoft YaHei UI", 17.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font f2 = new Font("Microsoft YaHei UI", 14.17f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font f3 = new Font("Microsoft YaHei UI", 18.33f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush fg = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                g.DrawString("文", f1, fg, 8, 4);
                g.DrawString("A", f2, fg, 18, 17);
                g.DrawString("翻译", f3, fg, 42, 9);
            }
            base.OnPaint(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Region 裁剪成圆角 (与旧版 97×39 形状一致, 右侧/下侧各留 1px)
            using (GraphicsPath path = RoundRect(new Rectangle(0, 0, 97, 39), 9))
            {
                Region = new Region(path);
            }
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
}
