// 自动拆分自 Program.cs - 模块: SettingsForms
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LLMTranslation
{

    // ---------------- JS 设置/使用说明 窗口 ----------------
    public class WebDialogForm : Form
    {
        private Microsoft.Web.WebView2.WinForms.WebView2 _web;
        private Panel body;
        private Panel edgeL;
        private Panel edgeR;
        private Panel edgeT;
        private Panel edgeB;
        private Panel crnTL;
        private Panel crnTR;
        private Panel crnBL;
        private Panel crnBR;
        private bool _edgeResizing;
        private int _edgeMask;
        private Rectangle _startRect;
        private Point _dragStart;
        private Config _cfg;
        private string _tab;
        public Action ConfigChanged;

        public WebDialogForm(Config cfg, string tab)
        {
            _cfg = cfg; _tab = tab;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = false;   // 不置顶, 避免抢占焦点导致划词取词失败
            BackColor = Color.White;
            DoubleBuffered = true;
            float s = Dpi.Scale;
            Size = new Size((int)(520 * s), (int)(620 * s));
            MinimumSize = new Size((int)(400 * s), (int)(460 * s));
            _web = new Microsoft.Web.WebView2.WinForms.WebView2();
            _web.Dock = DockStyle.Fill;

            // 边缘/角落透明覆盖层: 提供缩放光标 + 按下开始缩放 (同 TranslateForm;
            // 本窗不在鼠标钩子的 _translate 通道内, 面板 MouseDown 可直接触发)
            edgeL = MkEdge(Cursors.SizeWE, 1);
            edgeR = MkEdge(Cursors.SizeWE, 2);
            edgeT = MkEdge(Cursors.SizeNS, 4);
            edgeB = MkEdge(Cursors.SizeNS, 8);
            crnTL = MkEdge(Cursors.SizeNWSE, 5);
            crnBR = MkEdge(Cursors.SizeNWSE, 10);
            crnTR = MkEdge(Cursors.SizeNESW, 6);
            crnBL = MkEdge(Cursors.SizeNESW, 9);

            // 内衬容器: 四周留 6px 环带, 边缘覆盖层不与 WebView 抢鼠标
            body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(6);
            body.BackColor = Color.White;
            body.Controls.Add(_web);
            Controls.Add(body);
            Controls.Add(edgeL);
            Controls.Add(edgeR);
            Controls.Add(edgeT);
            Controls.Add(edgeB);
            Controls.Add(crnTL);
            Controls.Add(crnTR);
            Controls.Add(crnBL);
            Controls.Add(crnBR);
            edgeL.BringToFront();
            edgeR.BringToFront();
            edgeT.BringToFront();
            edgeB.BringToFront();
            crnTL.BringToFront();
            crnTR.BringToFront();
            crnBL.BringToFront();
            crnBR.BringToFront();
            SizeChanged += delegate { LayoutEdges(); };
            LayoutEdges();
            Icon = AppContext.GetSharedIcon();   // 任务栏图标 = 托盘同款蓝底"译"
            StartWeb();
        }

        private Panel MkEdge(Cursor cur, int mask)
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;
            p.Cursor = cur;
            p.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) BeginEdgeResize(mask);
            };
            return p;
        }

        private void LayoutEdges()
        {
            int W = ClientSize.Width;
            int H = ClientSize.Height;
            edgeL.Bounds = new Rectangle(0, 0, 6, H);
            edgeR.Bounds = new Rectangle(W - 6, 0, 6, H);
            edgeT.Bounds = new Rectangle(0, 0, W, 6);
            edgeB.Bounds = new Rectangle(0, H - 6, W, 6);
            crnTL.Bounds = new Rectangle(0, 0, 10, 10);
            crnTR.Bounds = new Rectangle(W - 10, 0, 10, 10);
            crnBL.Bounds = new Rectangle(0, H - 10, 10, 10);
            crnBR.Bounds = new Rectangle(W - 10, H - 10, 10, 10);
            if (!_edgeResizing)
            {
                using (GraphicsPath path = FloatingButtonForm.RoundRect(new Rectangle(0, 0, Width, Height), 12))
                {
                    this.Region = new Region(path);   // 圆角随尺寸重建 (拖动中保持直角, 松手后恢复)
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            // SetCapture 后, 移动/抬起消息路由到本窗体, 驱动边缘缩放
            if (m.Msg == 0x0200 && _edgeResizing) UpdateDrag(Cursor.Position);   // WM_MOUSEMOVE
            else if (m.Msg == 0x0202 && _edgeResizing) EndDrag();                 // WM_LBUTTONUP
            base.WndProc(ref m);
        }

        private void BeginEdgeResize(int mask)
        {
            _edgeMask = mask;
            _edgeResizing = true;
            _startRect = this.Bounds;
            _dragStart = Cursor.Position;
            // 同 TranslateForm: 拖动期间解除圆角 Region, 避免"窗口先变大、Region 后到"的透明闪烁带
            this.Region = null;
            NativeMethods.SetCapture(this.Handle);
        }

        private void UpdateDrag(Point p)
        {
            Rectangle r = _startRect;
            int dx = p.X - _dragStart.X;
            int dy = p.Y - _dragStart.Y;
            if ((_edgeMask & 1) != 0) { r.X = _startRect.X + dx; r.Width = _startRect.Width - dx; }
            if ((_edgeMask & 2) != 0) { r.Width = _startRect.Width + dx; }
            if ((_edgeMask & 4) != 0) { r.Y = _startRect.Y + dy; r.Height = _startRect.Height - dy; }
            if ((_edgeMask & 8) != 0) { r.Height = _startRect.Height + dy; }
            if (r.Width < MinimumSize.Width) { if ((_edgeMask & 1) != 0) r.X = r.Right - MinimumSize.Width; r.Width = MinimumSize.Width; }
            if (r.Height < MinimumSize.Height) { if ((_edgeMask & 4) != 0) r.Y = r.Bottom - MinimumSize.Height; r.Height = MinimumSize.Height; }
            this.Bounds = r;
        }

        private void EndDrag()
        {
            _edgeResizing = false;
            LayoutEdges();   // 松手恢复圆角裁剪
            NativeMethods.ReleaseCapture();
        }

        // FormBorderStyle=None 时 WinForms 不发 WM_SETICON (同 TranslateForm), 手动补三种槽位;
        // 槽位 0 空时资源管理器会回退到 exe 的默认 CS 窗口图标, 故必须显式设置
        private void ApplyWindowIcon()
        {
            try
            {
                IntPtr hicon = AppContext.GetSharedIcon().Handle;
                NativeMethods.SendMessage(this.Handle, 0x0080 /*WM_SETICON*/, new IntPtr(0 /*ICON_SMALL2*/), hicon);
                NativeMethods.SendMessage(this.Handle, 0x0080, new IntPtr(1 /*ICON_SMALL*/), hicon);
                NativeMethods.SendMessage(this.Handle, 0x0080, new IntPtr(2 /*ICON_BIG*/), hicon);
            }
            catch { }
        }

        // ---- 纯日志: 窗口状态诊断 (不改变任何行为) ----
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowIcon();
            WinLog.Window("settings-handle", this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWindowIcon();   // WinForms UpdateIcon 可能在显示时清空无边框窗图标, 再补一次
            WinLog.Window("settings-shown", this);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            WinLog.SizeChanged("settings", this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var path = FloatingButtonForm.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var pen = new Pen(Color.FromArgb(232, 232, 232))) e.Graphics.DrawPath(pen, path);
        }

        private void StartWeb()
        {
            // 竞态修复 (同 TranslateForm.StartWeb): WebView2 "热"时 env 续体可能早于
            // 句柄创建返回, 续体投递失败被丢 → 二次打开白屏。先强制同步创建句柄。
            var h = Handle;
            try
            {
                AppContext.Log("web(settings) env start shellDpi=" + NativeMethods.GetDpiForSystem());
                Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMTranslation", "WebView2Dialog"), null).ContinueWith(delegate (System.Threading.Tasks.Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment> et)
                {
                    // 续体可能在线程池线程执行, 控件操作必须在 UI 线程
                    AppContext.Log("web(settings) env done shellDpi=" + NativeMethods.GetDpiForSystem());
                    PostUI(delegate
                    {
                        if (et.IsFaulted || et.Result == null) { AppContext.Log("web(settings) env fault"); Close(); return; }
                        System.Threading.Tasks.Task t;
                        AppContext.Log("web(settings) ensure start shellDpi=" + NativeMethods.GetDpiForSystem());
                        try { t = _web.EnsureCoreWebView2Async(et.Result); }
                        catch { Close(); return; }
                    t.ContinueWith(delegate (System.Threading.Tasks.Task t2)
                    {
                        AppContext.Log("web(settings) ensure done shellDpi=" + NativeMethods.GetDpiForSystem());
                        PostUI(delegate
                        {
                            if (t2.IsFaulted || _web.CoreWebView2 == null) { Close(); return; }
                            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                            _web.CoreWebView2.WebMessageReceived += delegate(object s2, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e2)
                            { try { OnMsg(e2.TryGetWebMessageAsString()); } catch { } };
                            WinLog.Window("settings-webready", this);   // 纯日志: 页面加载时窗体实际状态
                            string path = Path.Combine(AppContext.WebRoot(), "settings.html");
                            if (File.Exists(path)) _web.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
                            else Close();   // web 资源缺失 (不应发生)
                            });
                        });
                    });
                });
            }
            catch { Close(); }
        }

        private void PostUI(Action a)
        {
            // 句柄已在 StartWeb 开头强制创建, 这里必走 BeginInvoke 直投。
            // (旧版在此用线程池 Timer 兜底重试, 但线程池无消息泵 Tick 永不触发,
            //  是白屏 bug 的直接机制, 已删)
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
            catch { }
        }

        private static string Q(string s) { return "\"" + Translator.JsonEscape(s ?? "") + "\""; }

        private void PushInit()
        {
            var c = _cfg;
            string json = "{\"ApiBase\":" + Q(c.ApiBase) + ",\"Port\":" + Q(c.Port) + ",\"Model\":" + Q(c.Model) +
                ",\"ApiKey\":" + Q(c.ApiKey) + ",\"Prompt\":" + Q(c.Prompt) + ",\"FontSize\":" + c.FontSize +
                ",\"EnableThinking\":" + (c.EnableThinking ? "true" : "false") +
                ",\"EnableSelection\":" + (c.EnableSelection ? "true" : "false") +
                ",\"AutoStart\":" + (c.AutoStart ? "true" : "false") +
                ",\"UiZoom\":" + c.UiZoom + ",\"ButtonDelay\":" + c.ButtonDelay + ",\"Hotkey\":" + Q(c.Hotkey) + "}";
            try { _web.ExecuteScriptAsync("if(window.init)init(" + json + ",\"" + _tab + "\")"); } catch { }
        }

        private void OnMsg(string msg)
        {
            if (!msg.StartsWith("{")) return;
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(msg)))
            {
                var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(WebMsg));
                var m = (WebMsg)ser.ReadObject(ms);
                if (m == null || m.t == null) return;
                if (m.t == "ready") { AppContext.Log("settings web ready dpr=" + (m.dpr > 0 ? m.dpr.ToString("0.##") : "?") + " win=" + Size); PushInit(); }
                else if (m.t == "close") Close();
                else if (m.t == "drag")
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(this.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                }
                else if (m.t == "save" && m.cfg != null)
                {
                    var d = m.cfg;
                    if (d.ApiBase != null) _cfg.ApiBase = d.ApiBase;
                    if (d.Port != null) _cfg.Port = d.Port;
                    if (d.Model != null) _cfg.Model = d.Model;
                    if (d.ApiKey != null) _cfg.ApiKey = d.ApiKey;
                    if (d.Prompt != null) _cfg.Prompt = d.Prompt;
                    if (d.FontSize >= 10 && d.FontSize <= 28) _cfg.FontSize = d.FontSize;
                    _cfg.EnableThinking = d.EnableThinking;
                    _cfg.EnableSelection = d.EnableSelection;
                    _cfg.AutoStart = d.AutoStart;
                    if (d.UiZoom >= 80 && d.UiZoom <= 200) _cfg.UiZoom = d.UiZoom;
                    if (d.ButtonDelay >= 0 && d.ButtonDelay <= 2000) _cfg.ButtonDelay = d.ButtonDelay;
                    if (!string.IsNullOrEmpty(d.Hotkey)) _cfg.Hotkey = d.Hotkey;
                    _cfg.Save();
                    AppContext.Log("SAVE enable=" + _cfg.EnableSelection + " delay=" + _cfg.ButtonDelay + " zoom=" + _cfg.UiZoom + " font=" + _cfg.FontSize);
                    ApplyAutoStart(_cfg.AutoStart);
                    if (ConfigChanged != null) ConfigChanged();
                    Close();
                }
            }
        }

        // 开机自启 (当前用户 Run 键, 无需管理员); 由 JS 设置的"开机自动启动"开关驱动
        internal static void ApplyAutoStart(bool on)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (on) key.SetValue("LLMTranslation", "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue("LLMTranslation", false);
                }
            }
            catch { }
        }
    }
}
