using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace ProjectEye.Core
{
    public class ShortcutHelper
    {
        private const string RunKeyName = "ProjectEye";
        private const string LnkFileName = "Project Eye.lnk";

        /// <summary>
        /// 为本程序创建一个快捷方式。
        /// </summary>
        private static bool CreateShortcut(string lnkFilePath, string args)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(lnkFilePath);
                shortcut.TargetPath = Assembly.GetEntryAssembly().Location;
                shortcut.Arguments = args;
                shortcut.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                shortcut.Save();
                return true;
            }
            catch (Exception e)
            {
                LogHelper.Warning(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 确保 StartupApproved 注册表中存在本程序的启动条目（ENABLED）。
        /// WScript.Shell COM 创建快捷方式后，Windows 不一定会自动注册到 StartupApproved，
        /// 导致开机启动静默失效。此方法手动补写注册表以修复该问题。
        /// </summary>
        private static void EnsureStartupApproved()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                    writable: true))
                {
                    if (key != null)
                    {
                        // 02 00 00 00 00 00 00 00 00 00 00 00 = ENABLED
                        byte[] enabled = { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                        key.SetValue(LnkFileName, enabled, RegistryValueKind.Binary);
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Warning("EnsureStartupApproved: " + e.ToString());
            }
        }

        /// <summary>
        /// 设置/移除注册表 Run 键（作为 Startup 文件夹的备用启动方式）。
        /// </summary>
        private static void SetRegistryRun(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                    writable: true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        string exePath = Assembly.GetEntryAssembly().Location;
                        key.SetValue(RunKeyName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunKeyName, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Warning("SetRegistryRun: " + e.ToString());
            }
        }

        /// <summary>
        /// 移除 StartupApproved 注册表条目。
        /// </summary>
        private static void RemoveStartupApproved()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                    writable: true))
                {
                    key?.DeleteValue(LnkFileName, throwOnMissingValue: false);
                }
            }
            catch (Exception e)
            {
                LogHelper.Warning("RemoveStartupApproved: " + e.ToString());
            }
        }

        /// <summary>
        /// 设置开机启动。同时使用 Startup 文件夹快捷方式 + 注册表 Run 键双重保障。
        /// </summary>
        public static bool SetStartup(bool startup = true)
        {
            string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    LnkFileName);
            if (startup)
            {
                // 每次都重新创建快捷方式，确保目标路径与当前 exe 位置一致
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                bool shortcutOk = CreateShortcut(path, "");

                // 双重保障：补写 StartupApproved 注册表 + 注册表 Run 键
                EnsureStartupApproved();
                SetRegistryRun(true);

                return shortcutOk;
            }
            else
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                RemoveStartupApproved();
                SetRegistryRun(false);
                return true;
            }
        }

        /// <summary>
        /// 创建桌面快捷方式
        /// </summary>
        public static bool CreateDesktopShortcut()
        {
            return CreateShortcut(Path.Combine(
                     Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                     LnkFileName), "");
        }
    }
}
