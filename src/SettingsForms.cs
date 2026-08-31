// 自动拆分自 Program.cs - 模块: SettingsForms
using System;
using System.Drawing;
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
            using (var rp = FloatingButtonForm.RoundRect(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(rp);  // 圆角窗口
            _web = new Microsoft.Web.WebView2.WinForms.WebView2();
            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);
            StartWeb();
        }

        // ---- 纯日志: 窗口状态诊断 (不改变任何行为) ----
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WinLog.Window("settings-handle", this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
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
            try
            {
                if (IsDisposed) return;
                if (IsHandleCreated) { BeginInvoke(a); return; }
                // 句柄尚未创建 (构造与 Show 之间的微小窗口): 短暂重试
                var tm = new System.Windows.Forms.Timer { Interval = 25 };
                tm.Tick += delegate
                {
                    tm.Stop();
                    if (IsDisposed) return;
                    if (IsHandleCreated) BeginInvoke(a);
                    else PostUI(a);
                };
                tm.Start();
            }
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
