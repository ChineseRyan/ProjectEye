using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace ProjectEye.Core
{
    public static class ScreenExtensions
    {
        public struct Dpi
        {
            public uint x { get; set; }
            public uint y { get; set; }

        }
        public static Dpi GetDpi(this System.Windows.Forms.Screen screen, DpiType dpiType)
        {
            Dpi dpi = new Dpi();
            try
            {
                var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
                var mon = MonitorFromPoint(pnt, 2/*MONITOR_DEFAULTTONEAREST*/);

                Win32APIHelper.RtlGetVersion(out Win32APIHelper.OsVersionInfo osVersionInfo);
                // GetDpiForMonitor 从 Windows 8.1 (6.3) 开始可用
                bool supportsPerMonitorDpi = osVersionInfo.MajorVersion > 6
                    || (osVersionInfo.MajorVersion == 6 && osVersionInfo.MinorVersion >= 3);
                if (supportsPerMonitorDpi)
                {
                    uint dpiX, dpiY;
                    GetDpiForMonitor(mon, dpiType, out dpiX, out dpiY);
                    dpi.x = dpiX;
                    dpi.y = dpiY;
                }
                else
                {
                    // Windows 7 / 8：所有显示器共享同一 DPI，使用系统 DPI 即可
                    using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    {
                        dpi.x = (uint)g.DpiX;
                        dpi.y = (uint)g.DpiY;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("ScreenExtensions.GetDpi: " + ex.ToString());
                dpi.x = 96;
                dpi.y = 96;
            }
            return dpi;

        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);
    }

    //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280511(v=vs.85).aspx
    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }
}
