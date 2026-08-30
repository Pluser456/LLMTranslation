// LLMTranslation 自解压安装包 (纯 .NET Framework 编译, 无第三方依赖)
// 内嵌资源: payload = 单文件 LLMTranslation.exe, uninst = Uninst.exe
// 安装位置: %LOCALAPPDATA%\Programs\LLMTranslation (免管理员, 每用户安装)
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LLMTranslationSetup
{
    internal static class Installer
    {
        private const string AppName = "LLMTranslation";
        private const string Version = "1.0.0";

        private static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool noLaunch = false;
            bool silent = false;
            foreach (string a in args)
            {
                if (a == "/NOSTART") noLaunch = true;
                if (a == "/S") silent = true;   // 静默: 不弹完成提示
            }
            try
            {
                // 已有实例在运行 (其他目录的副本) 时先结束, 避免文件占用
                try
                {
                    Process p = Process.Start(new ProcessStartInfo("taskkill.exe", "/IM " + AppName + ".exe /F")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    if (p != null) p.WaitForExit(3000);
                }
                catch { }
                System.Threading.Thread.Sleep(400);

                string dir = InstallDir;
                Directory.CreateDirectory(dir);
                string exePath = Path.Combine(dir, AppName + ".exe");
                string uninstPath = Path.Combine(dir, "Uninstall.exe");
                WriteResource("payload", exePath);
                WriteResource("uninst", uninstPath);

                // 快捷方式: 开始菜单(所有应用) + 桌面
                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk");
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
                CreateShortcut(startMenuLnk, exePath, dir, "LLM 划词翻译");
                CreateShortcut(desktopLnk, exePath, dir, "LLM 划词翻译");

                // 控制面板 "添加/删除程序" 条目
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LLMTranslation"))
                {
                    k.SetValue("DisplayName", "LLM 划词翻译");
                    k.SetValue("Publisher", "LLMTranslation");
                    k.SetValue("DisplayIcon", exePath);
                    k.SetValue("UninstallString", "\"" + uninstPath + "\"");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("Version", Version);
                    k.SetValue("InstallLocation", dir);
                }

                if (!noLaunch) Process.Start(exePath);

                if (silent) return;
                MessageBox.Show(
                    "LLM 划词翻译 安装完成\n\n" +
                    "安装位置: " + dir + "\n" +
                    "已创建: 开始菜单快捷方式 + 桌面快捷方式\n\n" +
                    "首次使用请在托盘图标菜单里配置 API 接口。",
                    "安装成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show("安装失败: " + e.Message, "LLM 划词翻译", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void WriteResource(string resName, string path)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
            {
                if (s == null) throw new Exception("内嵌资源缺失: " + resName);
                using (FileStream f = new FileStream(path, FileMode.Create, FileAccess.Write))
                    s.CopyTo(f);
            }
        }

        private static void CreateShortcut(string lnkPath, string targetPath, string workDir, string desc)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            Type t = shell.GetType();
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type lt = lnk.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { targetPath });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { desc });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }
    }
}
