// 自动拆分自 Program.cs - 模块: TrayHotkey
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LLMTranslation
{
    internal class HotkeyWindow : NativeWindow
    {
        public event EventHandler HotkeyPressed;
        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && HotkeyPressed != null) HotkeyPressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }
    }

    // ---------------- 主程序 / 托盘 / 划词检测 ----------------
    // 现代托盘菜单渲染器: 白底/圆角浅灰悬停/细分隔线, 逼近 Win11 原生菜单
    // 自绘托盘菜单: 保证文字完整不截断 + 每项均可悬停高亮 + DWM 原生圆角阴影
    internal class TrayMenuForm : Form
    {
        private class Item { public string Text; public bool Sep; public Action Act; }
        private readonly List<Item> _items = new List<Item>();
        private readonly List<Rectangle> _rects = new List<Rectangle>();
        private readonly Font _font;
        private readonly float _s;
        private int _hover = -1, _press = -1;

        public TrayMenuForm(float fontPx)
        {
            // DPI 感知进程: 设计像素布局统一 × Dpi.Scale (字体与所有布局常量同步), 物理尺寸与原 100% 时代一致
            _s = Dpi.Scale;
            _font = new Font("Microsoft YaHei UI", fontPx * _s, GraphicsUnit.Pixel);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(252, 252, 252);
            DoubleBuffered = true;
            Deactivate += delegate { Close(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WinLog.Window("traymenu", this);   // 纯日志: 菜单实际尺寸/位置/DPI
        }
        public TrayMenuForm Add(string text, Action act) { _items.Add(new Item { Text = text, Act = act }); return this; }
        public TrayMenuForm Sep() { _items.Add(new Item { Sep = true }); return this; }

        public void Prepare()
        {
            float s = _s;
            int contentW = 0, y = (int)(6 * s);
            _rects.Clear();
            var sizes = new List<Size>();
            foreach (var it in _items)
            {
                if (it.Sep) { sizes.Add(new Size(0, (int)(11 * s))); continue; }
                Size ms = TextRenderer.MeasureText(it.Text, _font);
                sizes.Add(ms);
                if (ms.Width + (int)(32 * s) > contentW) contentW = ms.Width + (int)(32 * s);
            }
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Sep) { _rects.Add(new Rectangle((int)(4 * s), y, 0, (int)(11 * s))); y += (int)(11 * s); }
                else { int ih = sizes[i].Height + (int)(14 * s); _rects.Add(new Rectangle((int)(4 * s), y, contentW, ih)); y += ih; }
            }
            this.Size = new Size(contentW + (int)(8 * s), y + (int)(8 * s));
        }

        public void ShowAtCursor()
        {
            var scr = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = Cursor.Position.X, yy = Cursor.Position.Y;
            if (x + Width > scr.Right) x = scr.Right - Width;
            if (yy + Height > scr.Bottom) yy = scr.Bottom - Height;
            Location = new Point(x, yy);
            Show();
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeMethods.ApplyFluentRound(Handle); }

        private int Hit(Point p)
        {
            for (int i = 0; i < _rects.Count; i++)
                if (!_items[i].Sep && _rects[i].Contains(p)) return i;
            return -1;
        }
        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != _hover) { _hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { if (_hover != -1) { _hover = -1; Invalidate(); } base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = Hit(e.Location); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            Action act = (h >= 0 && h == _press) ? _items[h].Act : null;
            _press = -1;
            if (act != null) { Close(); act(); }
            base.OnMouseUp(e);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(252, 252, 252));
            float s = _s;
            for (int i = 0; i < _items.Count; i++)
            {
                var r = _rects[i];
                if (_items[i].Sep)
                { using (var p = new Pen(Color.FromArgb(217, 217, 217))) g.DrawLine(p, (int)(12 * s), r.Y + r.Height / 2, Width - (int)(12 * s), r.Y + r.Height / 2); continue; }
                if (i == _hover)
                { using (var path = Round(new Rectangle(r.X, r.Y + (int)(1 * s), r.Width, r.Height - (int)(2 * s)), (int)(5 * s))) using (var b = new SolidBrush(_press == i ? Color.FromArgb(228, 228, 228) : Color.FromArgb(238, 238, 238))) g.FillPath(b, path); }
                TextRenderer.DrawText(g, _items[i].Text, _font, new Rectangle(r.X + (int)(16 * s), r.Y, r.Width - (int)(32 * s), r.Height), Color.FromArgb(26, 26, 26),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }
        private static GraphicsPath Round(Rectangle r, int rad)
        { var p = new GraphicsPath(); int d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    }

}
