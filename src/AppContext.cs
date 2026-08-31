// 自动拆分自 Program.cs - 模块: AppContext
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace LLMTranslation
{
    public class AppContext : ApplicationContext
    {
        private NotifyIcon _tray;
        private Config _config;
        private FloatingButtonForm _floating;
        private TranslateForm _translate;
        private SettingsForm _settings;
        private HotkeyWindow _hotkeyWin;
        private WinTimer _popupTimer;
        private WinTimer _hookWatchdog;
        // 后台取词线程: 所有慢调用(UIA/跨进程读/剪贴板)都在这里, UI 线程永不阻塞 => 低级钩子不会超时被摘除
        private Thread _worker;
        private readonly Queue<Action> _workerQueue = new Queue<Action>();
        private readonly object _workerLock = new object();
        private volatile int _extractGen;   // 取词任务代数: 点击别处后加一, 使排队中/进行中的旧任务作废
        private readonly SynchronizationContext _uiCtx;   // 回 UI 线程执行窗口操作 (不依赖胶囊句柄是否已创建)

        private NativeMethods.LowLevelMouseProc _hookProc;
        private IntPtr _hookPtr = IntPtr.Zero;
        private WinTimer _extractTimer;
        private Point _downPoint;
        private IntPtr _downFg;
        private IntPtr _lastForeign;
        private bool _downOnOwn;   // 按下时光标是否在自己窗口上 (标题栏拖动/边缘缩放/点自己 UI 都不应触发取词)
        private bool _isDown;
        private bool _dblClick;
        private int _lastUpTick;
        private Point _lastUpPoint;
        private Point _pendingPoint;
        private string _pendingText;
        private bool _forceNext;
        private string _lastText = "";
        private int _lastTick;
        private Bitmap _iconBmp;

        public AppContext()
        {
            _config = Config.Load();
            bool firstRun = !Config.Exists();

            _floating = new FloatingButtonForm();
            _floating.TranslateClicked += delegate
            {
                try
                {
                    Log("floating clicked");
                    _floating.HidePopup();
                    if (!string.IsNullOrEmpty(_pendingText))
                        _translate.ShowTranslate(_config, _pendingText, Cursor.Position);
                    Log("showtranslate done");
                }
                catch (Exception ex) { Log("click error: " + ex); }
            };
            _translate = new TranslateForm();
            _translate.Host = this;

            _uiCtx = SynchronizationContext.Current;
            WinLog.Startup();   // 纯日志: 启动时 DPI/监视器快照

            _extractTimer = new WinTimer();
            _extractTimer.Interval = 200;
            _extractTimer.Tick += delegate
            {
                _extractTimer.Stop();
                _extractTimer.Interval = 200;
                if (_isDown) { Log("extract skip: still dragging"); return; }   // 拖拽进行中不取词, 避免抓到半截选区弹野胶囊
                bool force = _forceNext;
                _forceNext = false;
                Point pt = _pendingPoint;
                int gen = ++_extractGen;   // 新任务 = 新代数; 旧任务发现代数不符即自行作废
                Log("extract enqueue gen=" + gen);
                EnqueueWork(delegate { TryExtract(pt, force, gen); });
            };

            StartWorker();

            SetupTray();
            InstallHook();

            // 钩子看门狗: 每 30s 重装一次低级钩子, 防止系统因超时静默摘除后永久失效
            _hookWatchdog = new WinTimer();
            _hookWatchdog.Interval = 5000;
            _hookWatchdog.Tick += delegate
            {
                ReinstallHook();
            };
            _hookWatchdog.Start();

            _popupTimer = new WinTimer();
            _popupTimer.Tick += delegate { _popupTimer.Stop(); _floating.Popup(_pendingPoint); };


            _hotkeyWin = new HotkeyWindow();
            ApplyHotkey();
            _hotkeyWin.HotkeyPressed += delegate
            {
                // 延迟 250ms 等用户松开 Alt, 再取词
                _pendingPoint = Cursor.Position;
                _forceNext = true;
                _extractTimer.Stop();
                _extractTimer.Interval = 250;
                _extractTimer.Start();
            };

            if (firstRun)
            {
                // 不用模态 MessageBox (被其他窗口挡住时会锁死整个应用), 直接非模态打开设置; 仅首次运行置顶
                OpenSettings();
                if (_dialog != null) { _dialog.TopMost = true; _dialog.BringToFront(); _dialog.Activate(); }
            }
        }

        private const string BuildTag = "v12dpi";    // v11log2 + 启动钉死 PMv2 感知 + TranslateForm 去双重缩放
        private static readonly bool DebugLog = Environment.GetEnvironmentVariable("LLMTRANSLATION_DEBUG") == "1";
        // 调试日志开关: 环境变量 LLMTRANSLATION_DEBUG=1, 或数据目录存在 debug.txt 文件 (删掉文件即关闭)
        private static readonly string DebugFlagFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMTranslation", "debug.txt");
        private static readonly int Pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        internal static void Log(string msg)
        {
            if (!DebugLog) { try { if (!File.Exists(DebugFlagFile)) return; } catch { return; } }
            try
            {
                string path = Path.Combine(DataRoot(), "log.txt");
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " [pid=" + Pid + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        // ---- 单文件部署: 全部数据/资源统一放 %LOCALAPPDATA%\LLMTranslation ----
        // exe 本身可以放在任何位置 (桌面/优盘/绿色目录), 设置与资源都跟着用户走
        internal static string DataRoot()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMTranslation");
            try { Directory.CreateDirectory(root); } catch { }
            return root;
        }

        internal static string WebRoot()
        {
            return Path.Combine(DataRoot(), "web");
        }

        private const string RunDepsVersion = "1";   // 内嵌 WebView2 依赖包版本 (变更时 +1 触发重新解包)
        private const string WebAssetsVersion = "5"; // 内嵌 web 资源版本 (变更时 +1 触发重新解包)

        private static bool ExtractZipIfChanged(string resName, string targetDir, string version)
        {
            string marker = Path.Combine(targetDir, ".llmt_ver");
            try
            {
                if (File.Exists(marker) && File.ReadAllText(marker).Trim() == version) return true;
            }
            catch { }
            try
            {
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (s == null) { Log("embedded resource missing: " + resName); return false; }
                    using (ZipArchive za = new ZipArchive(s, ZipArchiveMode.Read))   // .NET Framework 无 OpenRead(Stream) 重载
                    {
                        string rootFull = Path.GetFullPath(targetDir);
                        foreach (ZipArchiveEntry e in za.Entries)
                        {
                            string name = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                            string outPath = Path.GetFullPath(Path.Combine(targetDir, name));
                            if (!outPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) continue; // zip-slip 防护
                            if (name.EndsWith(Path.DirectorySeparatorChar.ToString()))
                            {
                                try { Directory.CreateDirectory(outPath); } catch { }
                                continue;
                            }
                            string dir = Path.GetDirectoryName(outPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            using (Stream fs = e.Open())
                            using (FileStream os = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                                fs.CopyTo(os);
                        }
                    }
                }
                File.WriteAllText(marker, version);
                Log("extracted " + resName + " ver=" + version);
                return true;
            }
            catch (Exception ex) { Log("extract failed " + resName + ": " + ex.Message); return false; }
        }

        // 启动时调用: 解包内嵌依赖与 web 资源, 并一次性迁移旧版 (exe 旁) 的 config.json
        internal static void EnsureRuntime()
        {
            ExtractZipIfChanged("rundeps", Path.Combine(DataRoot(), "run"), RunDepsVersion);
            ExtractZipIfChanged("webassets", WebRoot(), WebAssetsVersion);
            try
            {
                string newCfg = Path.Combine(DataRoot(), "config.json");
                string oldCfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(newCfg) && File.Exists(oldCfg)) File.Copy(oldCfg, newCfg, true);
            }
            catch { }
        }

        private void SetupTray()
        {
            _iconBmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_iconBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (GraphicsPath path = FloatingButtonForm.RoundRect(new Rectangle(1, 1, 30, 30), 8))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(70, 130, 210)))
                {
                    g.FillPath(brush, path);
                }
                using (Font f = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold))
                using (SolidBrush fg = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("译", f, fg, new RectangleF(0, 0, 32, 32), sf);
                }
            }
            Icon icon = Icon.FromHandle(_iconBmp.GetHicon());

            _tray = new NotifyIcon();
            _tray.Icon = icon;
            _tray.Text = "LLM 划词翻译";
            _tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left) ShowTrayMenu(); };
            _tray.DoubleClick += delegate { OpenDialog("set"); };
            _tray.Visible = true;
        }

        // 每次打开即时构建, 保证文字/开关状态最新且完整显示
        private void ShowTrayMenu()
        {
            float px = 13f * _config.UiZoom / 100f;
            var m = new TrayMenuForm(px);
            m.Add("划词翻译: " + (_config.EnableSelection ? "开" : "关"), delegate
            {
                _config.EnableSelection = !_config.EnableSelection;
                _config.Save();
                if (!_config.EnableSelection) _floating.HidePopup();
            });
            m.Add("设置 / 使用说明", delegate { OpenDialog("set"); });
            m.Sep();
            m.Add("退出", delegate { ExitApp(); });
            m.Prepare();
            m.ShowAtCursor();
        }

        private WebDialogForm _dialog;
        private void OpenDialog(string tab)
        {
            if (_dialog != null) { _dialog.BringToFront(); _dialog.Activate(); return; }
            _dialog = new WebDialogForm(_config, tab);
            _dialog.ConfigChanged += delegate { ApplyHotkey(); if (_translate != null) _translate.RefreshUi(); };
            _dialog.FormClosed += delegate { AppContext.Log("dialog closed"); _dialog = null; };
            _dialog.Show();
            _dialog.BringToFront();
            _dialog.Activate();
        }

        private void OpenSettings()
        {
            OpenDialog("set");
        }

        private void InstallHook()
        {
            try
            {
                _hookProc = MouseHookProc;
                _hookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, NativeMethods.GetModuleHandle(null), 0);
                AppContext.Log("InstallHook ptr=0x" + _hookPtr.ToString("x") + " build=" + BuildTag);
            }
            catch { }
        }

        // 看门狗: 重装钩子 (低级钩子一旦被系统超时摘除不会自动恢复)
        private void ReinstallHook()
        {
            try
            {
                if (_hookPtr != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hookPtr); _hookPtr = IntPtr.Zero; }
                _hookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, NativeMethods.GetModuleHandle(null), 0);
                AppContext.Log("hook reinstalled ptr=0x" + _hookPtr.ToString("x"));
            }
            catch { }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                NativeMethods.MSLLHOOKSTRUCT info = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                Point pt = new Point(info.pt.x, info.pt.y);
                if (msg == 0x0200) // WM_MOUSEMOVE: 驱动边缘缩放/分隔条
                {
                    if (_translate != null && _translate.DragActive) _translate.UpdateDrag(pt);
                }
                else if (msg == NativeMethods.WM_LBUTTONDOWN)
                {
                    if (_translate != null && _translate.Visible)
                    {
                        int em = _translate.EdgeMaskAt(pt);
                        if (em != 0) _translate.BeginEdgeResize(em);
                    }
                    _downPoint = pt;
                    _isDown = true;
                    Log("LBDN pt=" + pt + " inCap=" + (_floating != null && _floating.Bounds.Contains(pt)));
                    _downFg = NativeMethods.GetForegroundWindow();   // 记录按下时的前景窗, 供取词回退
                    {
                        NativeMethods.POINT dp = new NativeMethods.POINT { x = pt.X, y = pt.Y };
                        IntPtr du = NativeMethods.WindowFromPoint(dp);
                        IntPtr dr = du == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(du, NativeMethods.GA_ROOT);
                        if (dr == IntPtr.Zero) dr = du;
                        if (dr != IntPtr.Zero && !IsOwnWindow(dr)) _lastForeign = dr;   // 记录按下处外部窗, 翻译窗遮挡时回退
                        _downOnOwn = dr != IntPtr.Zero && IsOwnWindow(dr);
                    }
                    if (_floating != null && !_floating.Bounds.Contains(pt))
                    {
                        _floating.HidePopup();
                        // 点击别处 => 同时取消"待弹出"的取词/延迟弹出/剪贴板轮询, 避免点击后胶囊仍弹出
                        if (_extractTimer != null) _extractTimer.Stop();
                        if (_popupTimer != null) _popupTimer.Stop();
                        _extractGen++;      // 作废排队中/进行中的取词任务, 防止点击别处后胶囊仍弹出
                        Log("gen bump on outside click");
                    }
                }
                else if (msg == NativeMethods.WM_LBUTTONDBLCLK)
                {
                    _dblClick = true;
                }
                else if (msg == NativeMethods.WM_LBUTTONUP)
                {
                    bool wasDrag = _translate != null && _translate.DragActive;
                    if (wasDrag) _translate.EndDrag();
                    bool moved = _isDown && (Math.Abs(pt.X - _downPoint.X) > 8 || Math.Abs(pt.Y - _downPoint.Y) > 8);
                    // 低级钩子拿不到注入点击的 DBLCLK, 自行按 时间+距离 判定双击
                    int now = Environment.TickCount;
                    bool dbl = _dblClick || (!moved && !wasDrag && unchecked(now - _lastUpTick) < 500 &&
                        Math.Abs(pt.X - _lastUpPoint.X) < 10 && Math.Abs(pt.Y - _lastUpPoint.Y) < 10);
                    // 按下处在自己窗口 (标题栏拖动/边缘/分隔条/自己 UI) 一律不取词; 原生标题栏拖动不经过 DragActive 标志, 必须靠这里拦
                    bool sched = !_downOnOwn && !wasDrag && (moved || dbl);
                    AppContext.Log("LBUP moved=" + moved + " dbl=" + dbl + " own=" + _downOnOwn + " schedule=" + sched);
                    if (sched)
                    {
                        _pendingPoint = pt;
                        _extractTimer.Stop();
                        _extractTimer.Start();
                    }
                    _lastUpTick = now;
                    _lastUpPoint = pt;
                    _dblClick = false;
                    _isDown = false;
                }
            }
            return NativeMethods.CallNextHookEx(_hookPtr, nCode, wParam, lParam);
        }

        private bool IsOwnWindow(IntPtr hwnd)
        {
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        private static bool IsConsoleClass(string cls)
        {
            return cls == "ConsoleWindowClass" || cls == "CASCADIA_HOSTING_WINDOW_CLASS" ||
                   cls == "mintty" || cls == "PuTTY" || cls == "VTWindow" ||
                   cls == "VirtualConsoleClass" || cls == "WTWindow" || cls == "Vim32" || cls == "Vim";
        }

        private void TryExtract(Point pt, bool force, int gen)
        {
            Log("TryExtract force=" + force + " enable=" + _config.EnableSelection + " gen=" + gen);
            if (!force && !_config.EnableSelection) return;
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero || IsOwnWindow(fg))
            {
                // 前景被自己的置顶窗(翻译窗/设置窗)占用时, 改用鼠标位置下的外部窗口作为目标
                NativeMethods.POINT pp = new NativeMethods.POINT { x = pt.X, y = pt.Y };
                IntPtr under = NativeMethods.WindowFromPoint(pp);
                IntPtr root = under == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(under, NativeMethods.GA_ROOT);
                if (root == IntPtr.Zero) root = under;
                if (root != IntPtr.Zero && !IsOwnWindow(root)) fg = root;
                else if (_downFg != IntPtr.Zero && !IsOwnWindow(_downFg)) fg = _downFg;
                else if (_lastForeign != IntPtr.Zero && !IsOwnWindow(_lastForeign)) fg = _lastForeign;
            }
            Log("fg=0x" + fg.ToString("x") + " cls=" + (fg == IntPtr.Zero ? "" : NativeMethods.GetWindowClassName(fg)) + " own=" + (fg != IntPtr.Zero && IsOwnWindow(fg)));
            if (fg == IntPtr.Zero || IsOwnWindow(fg)) { Log("skip: own/none fg"); return; }
            string cls = NativeMethods.GetWindowClassName(fg);

            // 优先 UIA 从鼠标点读选区: 不切焦点、不碰剪贴板, 无副作用 (后台线程执行)
            string text = UiaSelectionText(pt);
            Log("uia=" + (text == null ? "null" : text.Length.ToString()));
            if (gen != _extractGen) return;   // 用户已点击别处, 旧任务作废
            if (string.IsNullOrEmpty(text)) text = Win32EditSelectionText(pt);
            Log("edit=" + (text == null ? "null" : text.Length.ToString()));
            if (gen != _extractGen) return;
            if (string.IsNullOrEmpty(text) && !IsConsoleClass(cls) && _config.UseClipboard)
            {
                // 剪贴板 Ctrl+C 需要目标窗为前景; 当前前景不是它则切换
                IntPtr cur = NativeMethods.GetForegroundWindow();
                if (cur != fg) { try { NativeMethods.SetForegroundWindow(fg); } catch { } }
                text = ClipboardSelectionText(gen);
            }
            if (gen != _extractGen) return;
            FinishExtract(text, pt, cls);
        }

        private void FinishExtract(string text, Point pt, string cls)
        {
            Log("text=" + (text == null ? "null" : text.Length.ToString()));
            if (text == null) return;
            text = text.Trim();
            if (text.Length < 2 || text.Length > 5000) { Log("skip: len"); return; }
            if (text == _lastText && unchecked(Environment.TickCount - _lastTick) < 400) { Log("skip: dup"); return; }
            _lastText = text;
            _lastTick = Environment.TickCount;
            _pendingText = text;
            Log("popup cls=" + cls);
            // 窗口显示必须在 UI 线程 (用 SynchronizationContext.Post, 不依赖胶囊句柄是否已创建)
            _uiCtx.Post(delegate(object state)
            {
                if (_config.ButtonDelay > 0) { _popupTimer.Stop(); _popupTimer.Interval = _config.ButtonDelay; _popupTimer.Start(); }
                else _floating.Popup(pt);
            }, null);
        }

        // 剪贴板取词(后台线程执行): 记录原剪贴板 -> 发 Ctrl+C -> 期间休眠轮询差异 -> 恢复原剪贴板
        private string ClipboardSelectionText(int gen)
        {
            try
            {
                Log("clip start");
                string before = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                IDataObject beforeObj = Clipboard.GetDataObject();
                // 先释放所有修饰键, 避免 Alt+Q 等热键场景下 Ctrl+C 变成 Ctrl+Alt+C
                NativeMethods.keybd_event(0x12, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero); // VK_MENU
                NativeMethods.keybd_event(0x10, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero); // VK_SHIFT
                NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event(NativeMethods.VK_C, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event(NativeMethods.VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                string after = null;
                for (int i = 0; i < 8; i++)
                {
                    Thread.Sleep(40);
                    Application.DoEvents();
                    if (gen != _extractGen) { Log("clip aborted (gen)"); break; }   // 用户已点击别处
                    after = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                    if (after != null && after != before) break;
                }
                try { if (beforeObj != null) Clipboard.SetDataObject(beforeObj); else Clipboard.Clear(); } catch { }
                Log("clip done=" + (after != null && after != before ? after.Length.ToString() : "null"));
                if (after != null && after != before) return after;
            }
            catch { }
            return null;
        }

        // 后台取词线程: STA(剪贴板要求) + 轻消息泵; 阻塞只发生在该线程, 不影响 UI 线程与低级钩子
        private void StartWorker()
        {
            _worker = new Thread(new ThreadStart(delegate
            {
                // 本线程要当剪贴板 owner 就必须在自身线程建一个顶层窗:
                // 系统的剪贴板所有权/延迟渲染消息 (WM_KILLCLIPBOARD / WM_RENDERFORMAT)
                // 只发给该线程的顶层窗; 裸线程无窗 => owner 永远无法响应 => OpenClipboard 卡 4-5s 或快速失败
                Form clipWin = new Form();
                clipWin.ShowInTaskbar = false;
                clipWin.FormBorderStyle = FormBorderStyle.None;
                clipWin.Size = new Size(1, 1);
                clipWin.Location = new Point(-32000, -32000);
                clipWin.CreateControl();   // 强制建句柄, 永不 Show (保持隐藏)

                while (true)
                {
                    Action job = null;
                    lock (_workerLock)
                    {
                        // 短切片等待(50ms): 本线程常是剪贴板 owner (每次复制/取词恢复后都是),
                        // owner 必须持续泵消息响应 WM_KILLCLIPBOARD/WM_RENDERFORMAT,
                        // 否则系统判定 owner 无响应 => 后续 OpenClipboard 卡 4-5s 或快速失败 => "剪贴板忙"
                        while (_workerQueue.Count == 0) Monitor.Wait(_workerLock, 50);
                        if (_workerQueue.Count > 0) job = _workerQueue.Dequeue();
                    }
                    if (job != null) { try { job(); } catch { } }
                    try { Application.DoEvents(); } catch { }
                    Thread.Sleep(10);
                }
            }));
            _worker.IsBackground = true;
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
        }

        // 入队后台工作线程; 队列满时返回 false (调用方可自行处理)
        private bool EnqueueWork(Action job)
        {
            lock (_workerLock)
            {
                if (_workerQueue.Count > 3) return false;   // 背压丢弃, 防止积压
                _workerQueue.Enqueue(job);
                Monitor.Pulse(_workerLock);
                return true;
            }
        }

        // 复制译文: 放到 STA 工作线程串行执行 (与取词的剪贴板 Ctrl+C/恢复天然无竞争),
        // 失败重试; 结果回 UI 线程回调。快速 划词→复制 循环下, UI 线程直接 SetText
        // 会与取词的剪贴板恢复互相覆盖, 导致"按了复制但没写进去"。
        // 物化写入: 用 Win32 SetClipboardData 把 UTF-16 文本存入全局内存块。
        // .NET Clipboard.SetText 的数据是 OLE 延迟渲染: 要粘贴时系统需向 owner 线程的顶层窗
        // 发消息取数, owner 无窗/不泵消息时 => "写入成功但粘不了、查看器也看不到"。
        // SetClipboardData 的数据全局立即可读, 与 owner 状态无关。
        private static bool RawSetClipboardText(string text)
        {
            if (!NativeMethods.OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                if (!NativeMethods.EmptyClipboard()) return false;
                char[] chars = text.ToCharArray();
                int bytes = (chars.Length + 1) * 2;   // UTF-16LE + 结尾 \0
                IntPtr hMem = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)bytes);
                if (hMem == IntPtr.Zero) return false;
                IntPtr p = NativeMethods.GlobalLock(hMem);
                if (p == IntPtr.Zero)
                {
                    NativeMethods.GlobalFree(hMem);
                    return false;
                }
                Marshal.Copy(chars, 0, p, chars.Length);
                Marshal.WriteInt16(new IntPtr(p.ToInt64() + chars.Length * 2), 0);
                NativeMethods.GlobalUnlock(hMem);
                if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hMem) == IntPtr.Zero)
                {
                    NativeMethods.GlobalFree(hMem);
                    return false;
                }
                return true;   // 成功后内存归系统所有, 不得再释放
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        public void CopyText(string text, Action<bool> done)
        {
            Action<bool> cb = done;
            bool queued = EnqueueWork(delegate
            {
                bool ok = false;
                int attempts = 0;
                for (int i = 0; i < 30 && !ok; i++)
                {
                    try
                    {
                        if (RawSetClipboardText(text)) ok = true;
                        else throw new Exception("raw open failed");
                    }
                    catch
                    {
                        attempts++;
                        // 重试前必须泵消息: 系统可能正在等本线程 (owner) 处理所有权交接消息,
                        // 只 Sleep 不泵 => 消息永远不处理 => 每次都失败 (自锁)
                        try { Application.DoEvents(); } catch { }
                        Thread.Sleep(100);
                    }
                }
                if (!ok)
                {
                    // 最终回退: .NET SetText (数据是延迟渲染, 但 worker 有隐藏窗+持续泵消息, 可渲染)
                    try { Clipboard.SetText(text); ok = true; } catch { }
                }
                Log("DoCopy " + (ok ? "ok" : ("FAILED after " + attempts + " retries")));
                if (cb != null && _uiCtx != null) _uiCtx.Post(delegate(object s) { try { cb(ok); } catch { } }, null);
            });
            if (!queued)
            {
                // 极端背压: 当前线程直接试一次, 不等待
                bool ok = false;
                try { Clipboard.SetText(text); ok = true; } catch { Log("DoCopy direct failed"); }
                if (cb != null) cb(ok);
            }
        }

        public void ApplyHotkey()
        {
            if (_hotkeyWin == null) return;
            NativeMethods.UnregisterHotKey(_hotkeyWin.Handle, 1);
            int mods, vk;
            if (ParseHotkey(_config.Hotkey, out mods, out vk))
            {
                bool ok = NativeMethods.RegisterHotKey(_hotkeyWin.Handle, 1, mods, vk);
                Log("hotkey " + _config.Hotkey + " registered=" + ok);
            }
        }

        private static bool ParseHotkey(string s, out int mods, out int vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split('+');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string m = parts[i].Trim().ToLower();
                if (m == "alt") mods |= 1;
                else if (m == "ctrl" || m == "control") mods |= 2;
                else if (m == "shift") mods |= 4;
                else if (m == "win" || m == "cmd") mods |= 8;
            }
            string k = parts[parts.Length - 1].Trim();
            if (k.Length == 1)
            {
                char c = char.ToUpper(k[0]);
                if (c >= 'A' && c <= 'Z') vk = (int)c;
                else if (c >= '0' && c <= '9') vk = (int)c;
                else return false;
            }
            else if (k.Length > 1 && (k[0] == 'F' || k[0] == 'f'))
            {
                int fn;
                if (int.TryParse(k.Substring(1), out fn) && fn >= 1 && fn <= 24) vk = 0x70 + fn - 1;
                else return false;
            }
            else return false;
            return vk != 0;
        }

        private static string UiaSelectionText(Point pt)
        {
            try
            {
                AutomationElement el = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
                int up = 0;
                while (el != null && up < 4)
                {
                    object pat;
                    if (el.TryGetCurrentPattern(TextPattern.Pattern, out pat))
                    {
                        TextPattern tp = pat as TextPattern;
                        if (tp != null)
                        {
                            TextPatternRange[] sel = tp.GetSelection();
                            if (sel != null && sel.Length > 0)
                            {
                                string t = sel[0].GetText(-1);
                                if (!string.IsNullOrEmpty(t)) return t;
                            }
                        }
                    }
                    el = TreeWalker.ControlViewWalker.GetParent(el);
                    up++;
                }
            }
            catch { }
            return null;
        }

        // legacy Win32 层: 跨进程读 Edit/RichEdit 选区, 不切焦点、不碰剪贴板
        private static string Win32EditSelectionText(Point pt)
        {
            try
            {
                NativeMethods.POINT pp = new NativeMethods.POINT { x = pt.X, y = pt.Y };
                IntPtr hwnd = NativeMethods.WindowFromPoint(pp);
                if (hwnd == IntPtr.Zero) return null;
                string cls = NativeMethods.GetWindowClassName(hwnd).ToUpperInvariant();
                if (!cls.Contains("EDIT")) return null;   // 覆盖 Edit / RICHEDIT50W / WindowsForms10.EDIT 等
                return ReadEditSelection(hwnd);
            }
            catch { }
            return null;
        }

        private static string ReadEditSelection(IntPtr hwnd)
        {
            uint pid; NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            IntPtr proc = NativeMethods.OpenProcess(NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_QUERY_INFORMATION, false, pid);
            if (proc == IntPtr.Zero) return null;
            IntPtr mem = IntPtr.Zero;
            try
            {
                mem = NativeMethods.VirtualAllocEx(proc, IntPtr.Zero, 8, NativeMethods.MEM_COMMIT, NativeMethods.PAGE_READWRITE);
                if (mem == IntPtr.Zero) return null;
                IntPtr res;
                Log("edit sel hwnd=0x" + hwnd.ToString("x"));
                if (NativeMethods.SendMessageTimeout(hwnd, NativeMethods.EM_GETSEL, mem, new IntPtr(mem.ToInt64() + 4), NativeMethods.SMTO_ABORTIFHUNG, 1500, out res) == 0) return null;
                byte[] buf = new byte[8]; uint rd;
                if (!NativeMethods.ReadProcessMemory(proc, mem, buf, 8, out rd)) return null;
                int start = BitConverter.ToInt32(buf, 0);
                int end = BitConverter.ToInt32(buf, 4);
                Log("edit sel start=" + start + " end=" + end);
                if (end <= start || start < 0) return null;
                uint len = NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 1500, out res);
                if (len <= 0 || len > 20000) return null;
                StringBuilder sb = new StringBuilder((int)len + 1);
                NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_GETTEXT, new IntPtr(len + 1), sb, NativeMethods.SMTO_ABORTIFHUNG, 1500, out res);
                string full = sb.ToString();
                if (end > full.Length) return null;
                return full.Substring(start, end - start);
            }
            finally
            {
                if (mem != IntPtr.Zero) NativeMethods.VirtualFreeEx(proc, mem, 0, NativeMethods.MEM_RELEASE);
                NativeMethods.CloseHandle(proc);
            }
        }

        private void ExitApp()
        {
            _extractTimer.Stop();
            if (_hookWatchdog != null) _hookWatchdog.Stop();
            if (_hookPtr != IntPtr.Zero) NativeMethods.UnhookWindowsHookEx(_hookPtr);
            if (_hotkeyWin != null) NativeMethods.UnregisterHotKey(_hotkeyWin.Handle, 1);
            _tray.Visible = false;
            _tray.Dispose();
            Application.Exit();
        }
    }

}
