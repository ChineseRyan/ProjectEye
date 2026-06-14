using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ProjectEye.Core
{
    /// <summary>
    /// 手动从嵌入资源中提取原生 DLL，解决 Costura.Fody 4.0 + Fody 5.0 
    /// 版本不匹配导致 SQLite.Interop.dll 运行时提取失败的问题。
    /// 
    /// Costura 将原生 DLL 嵌入为资源，但其 SetDllDirectory 钩子因 Fody 版本
    /// 不兼容而静默失效，P/Invoke 调用 sqlite3_open_interop 时找不到 DLL。
    /// 本类在应用启动时、任何 SQLite 操作之前，手动提取到临时目录并设置搜索路径。
    /// </summary>
    public static class NativeLoader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// 在应用启动时调用，提取原生 SQLite.Interop.dll 并确保 P/Invoke 能找到它。
        /// </summary>
        public static void Load()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string arch = Environment.Is64BitProcess ? "64" : "32";

                // MSBuild 嵌入资源的命名规则：{RootNamespace}.{folder}.{filename}
                // 路径分隔符 \ 变为 . 所以 costura32\SQLite.Interop.dll → ProjectEye.costura32.SQLite.Interop.dll
                string primaryName = $"ProjectEye.costura{arch}.sqlite.interop.dll";
                var allNames = assembly.GetManifestResourceNames();

                // 优先精确匹配，否则模糊搜索包含 sqlite.interop 的资源
                string resourceName = allNames.FirstOrDefault(n =>
                    n.Equals(primaryName, StringComparison.OrdinalIgnoreCase))
                    ?? allNames.FirstOrDefault(n =>
                    n.IndexOf("sqlite.interop", StringComparison.OrdinalIgnoreCase) >= 0
                    && n.Contains("costura" + arch));

                if (resourceName == null)
                {
                    LogHelper.Warning(
                        $"NativeLoader: embedded resource not found. Expected '{primaryName}'. " +
                        $"Available: {string.Join(", ", allNames)}");
                    return;
                }

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;

                    // 提取到临时目录（按版本哈希区分，避免多实例冲突）
                    string hash = assembly.GetName().Version.ToString();
                    string tempDir = Path.Combine(
                        Path.GetTempPath(), "ProjectEye", "native", hash);
                    Directory.CreateDirectory(tempDir);

                    string dllPath = Path.Combine(tempDir, "SQLite.Interop.dll");

                    // 仅在文件不存在或大小不匹配时重新提取
                    if (!File.Exists(dllPath) || new FileInfo(dllPath).Length != stream.Length)
                    {
                        using (var fileStream = File.Create(dllPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }

                    // 将临时目录添加到 DLL 搜索路径
                    SetDllDirectory(tempDir);

                    // 同时在应用目录放置 x86/x64 子目录（System.Data.SQLite 的标准搜索路径）
                    string subDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        Environment.Is64BitProcess ? "x64" : "x86");
                    Directory.CreateDirectory(subDir);

                    string targetPath = Path.Combine(subDir, "SQLite.Interop.dll");
                    if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != new FileInfo(dllPath).Length)
                    {
                        File.Copy(dllPath, targetPath, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warning("NativeLoader.Load failed: " + ex.ToString());
            }
        }
    }
}
