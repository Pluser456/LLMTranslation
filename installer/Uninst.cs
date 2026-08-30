// LLMTranslation 卸载器 (由 Installer.cs 安装时一并落盘)
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LLMTranslationSetup
{
    internal static class Uninst
    {
        private const string AppName = "LLMTranslation";

        private static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName); }
        }

        private static string StartMenuLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk"); }
        }

        private static string DesktopLnk
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk"); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = args != null && args.Length > 0 && args[0] == "/S";
            try
            {
                // 先结束应用, 避免文件被占用
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
                Thread.Sleep(500);

                // 删除安装目录中的主程序 (卸载器自身正在运行, 无法自删, 见下方延迟清理)
                try
                {
                    string main = Path.Combine(InstallDir, AppName + ".exe");
                    if (File.Exists(main)) { File.SetAttributes(main, FileAttributes.Normal); File.Delete(main); }
                }
                catch { }

                // 删除快捷方式 (.lnk 就是普通文件, 直接删 + 重试)
                DeleteShortcut(StartMenuLnk);
                DeleteShortcut(DesktopLnk);

                // 删除 添加/删除程序 注册表项
                try { Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LLMTranslation"); } catch { }

                // 自删: 本进程无法删除自己正在运行的 exe, 用延迟 cmd 清理残留
                try
                {
                    string delCmd = "/c timeout /t 1 /nobreak >nul & del \"" + Path.Combine(InstallDir, "Uninstall.exe") + "\" & rmdir \"" + InstallDir + "\"";
                    Process.Start(new ProcessStartInfo("cmd.exe", delCmd)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch { }

                if (silent) return;
                MessageBox.Show(
                    "LLM 划词翻译 已卸载。\n\n" +
                    "设置与缓存保留在 %LOCALAPPDATA%\\LLMTranslation (重新安装可沿用),\n如需彻底清理请手动删除该目录。",
                    "卸载完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show("卸载出错: " + e.Message, "LLM 划词翻译", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void DeleteShortcut(string lnkPath)
        {
            for (int i = 0; i < 5; i++)
            {
                try { if (!File.Exists(lnkPath)) return; File.Delete(lnkPath); return; }
                catch { Thread.Sleep(200); }
            }
        }
    }
}
