// LLMTranslation - 程序入口
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LLMTranslation
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            Mutex mutex = new Mutex(true, "LLMTranslation_SingleInstance", out created);
            if (!created) return;
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                AppContext.Log("UNHANDLED: " + e.ExceptionObject);
            };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                AppContext.Log("THREAD_EXCEPTION: " + e.Exception);
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            try
            {
                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            }
            catch { }
            try
            {
                string udf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMTranslation", "WebView2");
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", udf);
            }
            catch { }
            // 单文件模式: 解包内嵌资源 (web 前端 + WebView2 依赖) 到 %LOCALAPPDATA%\LLMTranslation
            try { AppContext.EnsureRuntime(); } catch { }
            // 原生 WebView2Loader.dll 的搜索路径包含当前目录 => 指向解包出的 run\ 目录
            try { Environment.CurrentDirectory = Path.Combine(AppContext.DataRoot(), "run"); } catch { }
            // 托管 WebView2 程序集不在 exe 旁时, 从 run\ 目录回退加载
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs a)
            {
                try
                {
                    string name = new System.Reflection.AssemblyName(a.Name).Name;
                    string p = Path.Combine(AppContext.DataRoot(), "run", name + ".dll");
                    if (File.Exists(p)) return System.Reflection.Assembly.LoadFile(p);
                }
                catch { }
                return null;
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var app = new AppContext();
            // 纯日志: 显示器/显示设置变化事件 (不改变任何行为)
            Application.AddMessageFilter(new DisplayLogFilter());
            Application.Run(app);
            GC.KeepAlive(mutex);
        }

        // 显示器/显示设置变化事件日志: 与窗口状态日志对照, 可定位"某时刻缩放被改"
        private sealed class DisplayLogFilter : System.Windows.Forms.IMessageFilter
        {
            private int _lastSettingLog;
            public bool PreFilterMessage(ref System.Windows.Forms.Message m)
            {
                try
                {
                    if (m.Msg == 0x007E)  // WM_DISPLAYCHANGE: 分辨率/监视器变化
                        AppContext.Log("DISPLAY: WM_DISPLAYCHANGE (分辨率/监视器变化)");
                    else if (m.Msg == 0x001A)  // WM_SETTINGCHANGE: 较频繁, 限流 5s 一条
                    {
                        int now = Environment.TickCount;
                        if (now - _lastSettingLog > 5000) { _lastSettingLog = now; AppContext.Log("DISPLAY: WM_SETTINGCHANGE"); }
                    }
                }
                catch { }
                return false;
            }
        }
    }
}
