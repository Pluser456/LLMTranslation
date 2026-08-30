// 自动拆分自 Program.cs - 模块: TranslateForm
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace LLMTranslation
{
    public class TranslateForm : Form
    {
        private Panel body;
        private TextBox txtResult;   // WebView2 不可用时的回退显示
        private Microsoft.Web.WebView2.WinForms.WebView2 _web;
        private Panel toast;
        private Panel edgeL;
        private Panel edgeR;
        private Panel edgeT;
        private Panel edgeB;
        private Panel crnTL;
        private Panel crnTR;
        private Panel crnBL;
        private Panel crnBR;
        private WinTimer toastTimer;
        private Label lblToast;
        private WinTimer _pushTimer;

        public AppContext Host;   // 由 AppContext 构造后注入, 用于把剪贴板操作派发到 STA 工作线程
        private bool _webReady;   // CoreWebView2 已初始化
        private bool _webFailed;
        private bool _webInitStarted;
        private StringBuilder _sb = new StringBuilder();
        private CancellationTokenSource _cts;
        private bool _hadError;
        private bool _pinned = true;
        private Config _cfg;
        private string _lastText;
        private bool _keepPos;
        private int _chunkCount;
        private int _pushCount;

        private int _edgeMask;
        private bool _edgeResizing;
        private Rectangle _startRect;
        private Point _dragStart;

        public bool DragActive { get { return _edgeResizing; } }

        public static string LangName(string code)
        {
            if (code == "zh") return "简体中文";
            if (code == "en") return "English";
            return "自动";
        }

        public TranslateForm()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(520, 400);
            MinimumSize = new Size(320, 240);
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9f);
            Text = "LLMT_TRANSLATE";   // 稳定标题, 供自动化测试定位

            // ---- 译文区 ----
            txtResult = new TextBox();
            txtResult.Multiline = true;
            txtResult.ReadOnly = true;
            txtResult.BorderStyle = BorderStyle.None;
            txtResult.ScrollBars = ScrollBars.Vertical;
            txtResult.Dock = DockStyle.Fill;
            txtResult.Font = new Font("Microsoft YaHei UI", 10f);
            txtResult.WordWrap = true;

            _web = new Microsoft.Web.WebView2.WinForms.WebView2();
            _web.Dock = DockStyle.Fill;
            _pushTimer = new WinTimer();
            _pushTimer.Interval = 150;
            _pushTimer.Tick += delegate { _pushTimer.Stop(); PushNow(); };

            // ---- 复制成功 toast ----
            toast = new Panel();
            toast.Size = new Size(150, 36);
            toast.BackColor = Color.White;
            Label lblCheck = new Label();
            lblCheck.Text = "✓";
            lblCheck.ForeColor = Color.FromArgb(40, 170, 90);
            lblCheck.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lblCheck.AutoSize = true;
            lblCheck.Location = new Point(14, 8);
            lblToast = new Label();
            lblToast.Text = "复制成功";
            lblToast.AutoSize = true;
            lblToast.Location = new Point(38, 10);
            toast.Controls.Add(lblCheck);
            toast.Controls.Add(lblToast);
            toast.Visible = false;
            toastTimer = new WinTimer();
            toastTimer.Interval = 1500;
            toastTimer.Tick += delegate { toastTimer.Stop(); toast.Visible = false; };

            // 边缘/角落透明覆盖层: 提供缩放光标 + 按下开始缩放
            edgeL = MkEdge(Cursors.SizeWE, 1);
            edgeR = MkEdge(Cursors.SizeWE, 2);
            edgeT = MkEdge(Cursors.SizeNS, 4);
            edgeB = MkEdge(Cursors.SizeNS, 8);
            crnTL = MkEdge(Cursors.SizeNWSE, 5);
            crnBR = MkEdge(Cursors.SizeNWSE, 10);
            crnTR = MkEdge(Cursors.SizeNESW, 6);
            crnBL = MkEdge(Cursors.SizeNESW, 9);

            // 内衬容器: 四周留 6px 环带露出窗体背景, 让边缘覆盖层不与 WebView/标题栏 抢鼠标
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
            Controls.Add(toast);
            toast.BringToFront();
            SizeChanged += delegate { LayoutCustom(); };
            LayoutCustom();
            toast.Visible = false;
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

        private void LayoutCustom()
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
            toast.Location = new Point((ClientSize.Width - toast.Width) / 2, ClientSize.Height - toast.Height - 14);
            ApplyRoundRegion();
        }

        private void ApplyRoundRegion()
        {
            using (GraphicsPath path = FloatingButtonForm.RoundRect(new Rectangle(0, 0, Width, Height), 12))
            {
                this.Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (GraphicsPath path = FloatingButtonForm.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (Pen pen = new Pen(Color.FromArgb(232, 232, 232)))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        // ---- 纯日志: 窗口状态诊断 (不改变任何行为) ----
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WinLog.Window("translate-handle", this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WinLog.Window("translate-shown", this);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            WinLog.SizeChanged("translate", this);
        }

        protected override void WndProc(ref Message m)
        {
            // SetCapture 后, 移动/抬起消息路由到本窗体, 驱动边缘缩放/分隔条
            if (m.Msg == 0x0200 && _edgeResizing) UpdateDrag(Cursor.Position);   // WM_MOUSEMOVE
            else if (m.Msg == 0x0202 && _edgeResizing) EndDrag();                 // WM_LBUTTONUP
            base.WndProc(ref m);
        }

        // ---- 钩子驱动的边缘缩放 / 分隔条拖动 ----
        public int EdgeMaskAt(Point p)
        {
            Rectangle r = this.Bounds;
            const int b = 6;
            bool near = p.X >= r.X - 2 && p.X <= r.Right + 1 && p.Y >= r.Y - 2 && p.Y <= r.Bottom + 1;
            if (!near) return 0;
            int mask = 0;
            if (p.X <= r.X + b) mask |= 1;
            if (p.X >= r.Right - b) mask |= 2;
            if (p.Y <= r.Y + b) mask |= 4;
            if (p.Y >= r.Bottom - b) mask |= 8;
            return mask;
        }

        public void BeginEdgeResize(int mask)
        {
            _edgeMask = mask;
            _edgeResizing = true;
            _startRect = this.Bounds;
            _dragStart = Cursor.Position;
            NativeMethods.SetCapture(this.Handle);   // 捕获鼠标, 移动/抬起在 WndProc 处理, 不受子窗口影响
        }

        public void UpdateDrag(Point p)
        {
            if (_edgeResizing)
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
        }

        public void EndDrag()
        {
            _edgeResizing = false;
            NativeMethods.ReleaseCapture();
        }

        // ---- WebView2 ----
        public void WarmUp() { StartWeb(); }   // 启动时预热 WebView2, 首次打开翻译窗无冷启动

        private void StartWeb()
        {
            if (_webInitStarted) return;
            _webInitStarted = true;
            try
            {
                AppContext.Log("web(translate) env start shellDpi=" + NativeMethods.GetDpiForSystem());
                Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, null, null).ContinueWith(delegate(Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment> et)
                {
                    // 续体可能在线程池线程执行, 控件操作必须在 UI 线程
                    AppContext.Log("web(translate) env done shellDpi=" + NativeMethods.GetDpiForSystem());
                    Post(delegate
                    {
                        if (et.IsFaulted || et.Result == null)
                        {
                            AppContext.Log("web env fault: " + (et.Exception == null ? "?" : et.Exception.Message));
                            Fallback(); return;
                        }
                        Task t;
                        AppContext.Log("web(translate) ensure start shellDpi=" + NativeMethods.GetDpiForSystem());
                        try { t = _web.EnsureCoreWebView2Async(et.Result); }
                        catch (Exception ex) { AppContext.Log("web init fault: " + ex.Message); Fallback(); return; }
                    t.ContinueWith(delegate(Task t2)
                    {
                        AppContext.Log("web(translate) ensure done shellDpi=" + NativeMethods.GetDpiForSystem());
                        Post(delegate
                        {
                            if (t2.IsFaulted || _web.CoreWebView2 == null)
                            {
                                AppContext.Log("web init fault: " + (t2.IsFaulted ? (t2.Exception == null ? "?" : t2.Exception.ToString()) : "null core"));
                                Fallback(); return;
                            }
                        _webReady = true;
                        try
                        {
                            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; // 禁用 WebView 默认右键菜单
                            _web.CoreWebView2.WebMessageReceived += delegate(object s2, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e2)
                            {
                                try { HandleWebMsg(e2.TryGetWebMessageAsString()); }
                                catch { }
                            };
                        }
                        catch { }
                        string path = Path.Combine(AppContext.WebRoot(), "index.html");
                        if (File.Exists(path))
                        {
                            AppContext.Log("web nav: " + path);
                            _web.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
                        }
                        else
                        {
                            AppContext.Log("web index.html MISSING: " + path);
                            Fallback();
                        }
                        PushNow();
                            });
                        });
                    });
                });
            }
            catch (Exception ex) { AppContext.Log("web start exception: " + ex.Message); Fallback(); }
        }

        private void Fallback()
        {
            if (_webFailed) return;
            _webFailed = true;
            _webReady = false;
            try
            {
                if (_web.Parent != null) Controls.Remove(_web);
                _web.Dispose();
            }
            catch { }
            if (txtResult.Parent == null) Controls.Add(txtResult);
            txtResult.Text = _sb.Length > 0 ? _sb.ToString() : "翻译中…";
        }

        public Action ConfigChanged;

        public void RefreshUi() { PushInit(); PushFont(); }   // 设置变更后立即刷新界面(缩放/字体)

        private static string Q(string s) { return "\"" + Translator.JsonEscape(s ?? "") + "\""; }

        private void StartDrag()
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(this.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
        }

        private void PushInit()
        {
            if (_webFailed || !_webReady || _cfg == null) return;
            var c = _cfg;
            string json = "{\"ApiBase\":" + Q(c.ApiBase) + ",\"Port\":" + Q(c.Port) + ",\"Model\":" + Q(c.Model) +
                ",\"ApiKey\":" + Q(c.ApiKey) + ",\"Prompt\":" + Q(c.Prompt) +
                ",\"FontSize\":" + c.FontSize + ",\"EnableThinking\":" + (c.EnableThinking ? "true" : "false") +
                ",\"EnableSelection\":" + (c.EnableSelection ? "true" : "false") +
                ",\"TargetLang\":" + Q(c.TargetLang) + ",\"SourceLang\":" + Q(c.SourceLang) +
                ",\"ShowOriginal\":" + (c.ShowOriginal ? "true" : "false") +
                ",\"UiZoom\":" + c.UiZoom +
                ",\"Pinned\":" + (_pinned ? "true" : "false") + "}";
            try { _web.ExecuteScriptAsync("if(window.init)init(" + json + ")"); } catch { }
        }

        private void PushOrig()
        {
            if (_webFailed || !_webReady || _cfg == null) return;
            try { _web.ExecuteScriptAsync("if(window.setOrig)setOrig(\"" + Translator.JsonEscape(_lastText ?? "") + "\"," + (_cfg.ShowOriginal ? "true" : "false") + ")"); } catch { }
        }

        private void PushState(bool s)
        {
            if (_webFailed || !_webReady) return;
            try { _web.ExecuteScriptAsync("if(window.setState)setState(" + (s ? "true" : "false") + ")"); } catch { }
        }

        private void HandleWebMsg(string msg)
        {
            if (!msg.StartsWith("{")) return;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(msg)))
                {
                    var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(WebMsg));
                    var m = (WebMsg)ser.ReadObject(ms);
                    if (m == null || m.t == null) return;
                    switch (m.t)
                    {
                        case "drag": StartDrag(); break;
                        case "esc": CloseTranslation(); break;
                        case "close": CloseTranslation(); break;
                        case "pin": _pinned = !_pinned; TopMost = _pinned; PushInit(); break;
                        case "copy": DoCopy(); break;
                        case "retranslate": Retranslate(); break;
                        case "ready": AppContext.Log("web ready dpr=" + (m.dpr > 0 ? m.dpr.ToString("0.##") : "?") + " win=" + Size); PushInit(); PushOrig(); PushNow(); break;   // JS 以 JSON 发送
                        case "pause": if (_cts != null) { _cts.Cancel(); _cts = null; PushState(false); } break;
                        case "orig": if (_cfg != null) { _cfg.ShowOriginal = !_cfg.ShowOriginal; _cfg.Save(); PushOrig(); } break;
                        case "lang": if (_cfg != null) { _cfg.TargetLang = m.tgt ?? "auto"; _cfg.Save(); PushInit(); Retranslate(); } break;
                        case "setconfig": ApplyConfig(m.cfg); break;
                    }
                }
            }
            catch { }
        }

        private void ApplyConfig(CfgDto d)
        {
            if (_cfg == null || d == null) return;
            if (d.ApiBase != null) _cfg.ApiBase = d.ApiBase;
            if (d.Port != null) _cfg.Port = d.Port;
            if (d.Model != null) _cfg.Model = d.Model;
            if (d.ApiKey != null) _cfg.ApiKey = d.ApiKey;
            if (d.Prompt != null) _cfg.Prompt = d.Prompt;
            if (d.FontSize >= 10 && d.FontSize <= 28) _cfg.FontSize = d.FontSize;
            _cfg.EnableThinking = d.EnableThinking;
            _cfg.EnableSelection = d.EnableSelection;
            _cfg.Save();
            PushFont(); PushInit();
            if (ConfigChanged != null) ConfigChanged();
            Retranslate();
        }

        private void PushFont()
        {
            if (_webFailed || !_webReady || _cfg == null) return;
            try { _web.ExecuteScriptAsync("if(window.setFont)setFont(" + _cfg.FontSize + ")"); } catch { }
        }

        private void PushText(string md, bool final)
        {
            if (_webFailed || !_webReady) return;
            try
            {
                _web.ExecuteScriptAsync("if(window.setContent)setContent(\"" + Translator.JsonEscape(md) + "\"," + (final ? "true" : "false") + ")");
            }
            catch { }
        }

        private void PushNow()
        {
            _pushCount++;
            if (_pushCount % 5 == 1) AppContext.Log("push #" + _pushCount + " len=" + _sb.Length + " ready=" + _webReady);
            PushText(_sb.ToString(), false);
        }

        private void SchedulePush()
        {
            if (_webFailed || !_webReady) return;
            // 节流: 计时器在跑就不重置, 保证连续出字时也能每 150ms 推送一次
            if (!_pushTimer.Enabled) _pushTimer.Start();
        }

        private void Post(Action a)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(a);
            }
            catch { }
        }

        public void CloseTranslation()
        {
            if (_cts != null) { _cts.Cancel(); _cts = null; }
            _pushTimer.Stop();
            Hide();
        }

        private void DoCopy()
        {
            AppContext.Log("DoCopy enter webReady=" + _webReady + " sbLen=" + _sb.Length);
            string t = _webFailed ? txtResult.Text : _sb.ToString();
            if (string.IsNullOrEmpty(t) || t == "翻译中…") return;
            string text = t;
            if (Host != null)
                Host.CopyText(text, delegate(bool ok) { PushToast(ok, ok ? "✓ 已复制到剪贴板" : "✗ 复制失败，剪贴板忙，请再按一次 C"); });
            else
            {
                bool ok = false;
                try { Clipboard.SetText(text); ok = true; } catch { }
                PushToast(ok, ok ? "✓ 已复制到剪贴板" : "✗ 复制失败，请再按一次 C");
            }
        }

        // 成功/失败反馈: WebView 模式走 JS toast, 回退模式点亮 C# toast 面板
        private void PushToast(bool ok, string msg)
        {
            try
            {
                if (_webReady && !_webFailed)
                    _web.ExecuteScriptAsync("if(window.toast)toast(\"" + Translator.JsonEscape(msg) + "\"," + (ok ? "true" : "false") + ")");
                else
                {
                    lblToast.Text = msg;
                    lblToast.ForeColor = ok ? Color.FromArgb(40, 167, 90) : Color.FromArgb(214, 69, 69);
                    toast.Visible = true;
                    toastTimer.Stop();
                    toastTimer.Start();
                }
            }
            catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { CloseTranslation(); e.Handled = true; }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.C) { DoCopy(); e.Handled = true; }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.R) { Retranslate(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        public void Retranslate()
        {
            if (_cfg == null || _lastText == null) return;
            _keepPos = true;
            ShowTranslate(_cfg, _lastText, Location);   // 锚点被 _keepPos 忽略, 传当前位置即可
        }

        public void ShowTranslate(Config cfg, string text, Point anchor)
        {
            _cfg = cfg;
            _lastText = text;
            AppContext.Log("ShowTranslate enter len=" + text.Length);
            if (_cts != null) { _cts.Cancel(); }
            _cts = new CancellationTokenSource();
            _sb.Length = 0;
            _chunkCount = 0;
            _pushCount = 0;
            _hadError = false;
            txtResult.Font = new Font("Microsoft YaHei UI", cfg.FontSize);
            txtResult.Text = "翻译中…";
            StartWeb();
            if (_webReady) { PushFont(); PushInit(); PushOrig(); PushState(true); PushText("翻译中…", false); }

            if (!_keepPos)
            {
                Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
                int x = anchor.X + 14;
                int y = anchor.Y - 40;
                if (x + Width > wa.Right) x = anchor.X - Width - 14;
                if (x < wa.Left) x = wa.Left + 4;
                if (y + Height > wa.Bottom) y = wa.Bottom - Height - 4;
                if (y < wa.Top) y = wa.Top + 4;
                Location = new Point(x, y);
            }
            _keepPos = false;

            if (!Visible) Show();
            Activate();
            BringToFront();

            CancellationToken token = _cts.Token;
            Translator.TranslateAsync(cfg, text,
                chunk =>
                {
                    Post(delegate
                    {
                        _sb.Append(chunk);
                        _chunkCount++;
                        if (_chunkCount % 10 == 1) AppContext.Log("chunk #" + _chunkCount + " len=" + _sb.Length);
                        if (_webFailed)
                        {
                            txtResult.Text = _sb.ToString();
                            txtResult.SelectionStart = txtResult.Text.Length;
                            txtResult.ScrollToCaret();
                        }
                        else SchedulePush();
                    });
                },
                err =>
                {
                    Post(delegate
                    {
                        _hadError = true;
                        if (_webFailed) txtResult.Text = "⚠ 请求失败: " + err;
                        else PushText("⚠ 请求失败: " + err, true);
                        PushState(false);
                    });
                },
                () =>
                {
                    Post(delegate
                    {
                        AppContext.Log("ui done hadError=" + _hadError + " len=" + _sb.Length);
                        if (!_hadError && _sb.Length == 0)
                        {
                            if (_webFailed) txtResult.Text = "（模型未返回内容）";
                            else PushText("（模型未返回内容）", true);
                        }
                        else PushText(_sb.ToString(), true);   // 结束: 精排 Markdown+公式
                        PushState(false);
                        WinTimer diagTimer = new WinTimer();
                        diagTimer.Interval = 2500;
                        diagTimer.Tick += delegate
                        {
                            diagTimer.Stop();
                            diagTimer.Dispose();
                            if (!_webFailed && _webReady)
                            {
                                try { _web.ExecuteScriptAsync("if(window.postDiag)postDiag()"); } catch { }
                            }
                        };
                        diagTimer.Start();
                    });
                },
                token);
        }
    }

    // ---------------- 设置窗口 ----------------
}

