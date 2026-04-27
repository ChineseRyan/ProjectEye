using ProjectEye.Core;
using ProjectEye.Core.Service;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ProjectEye
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {

        private readonly ServiceCollection serviceCollection;
        private System.Threading.Mutex mutex;
        public delegate void AppEventHandler();
        /// <summary>
        /// 服务初始化完成时发生
        /// </summary>
        public event AppEventHandler OnServiceInitialized;
        public App()
        {
            serviceCollection = new ServiceCollection();

            // 注册全局未处理异常（非 UI 线程异常）
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 注册 Task 中未观察到的异常
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            //全局异常捕获（UI 线程）
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            //重复运行判断
            if (IsRuned())
            {
                //仅允许运行一次进程
                MessageBox.Show("程序已经在运行中了", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                App.Current.Shutdown();
            }
            else
            {
                try
                {
                    //必须按优先级依次添加
                    serviceCollection.AddInstance(this);
                    //后台任务
                    serviceCollection.Add<BackgroundWorkerService>();
                    //数据统计
                    serviceCollection.Add<StatisticService>();
                    //系统资源
                    serviceCollection.Add<SystemResourcesService>();
                    //内存缓存
                    serviceCollection.Add<CacheService>();
                    //配置文件
                    serviceCollection.Add<ConfigService>();
                    //主题
                    serviceCollection.Add<ThemeService>();
                    //扩展显示器
                    serviceCollection.Add<ScreenService>();
                    //主要
                    serviceCollection.Add<MainService>();
                    //托盘
                    serviceCollection.Add<TrayService>();
                    //休息
                    serviceCollection.Add<RestService>();
                    //声音
                    serviceCollection.Add<SoundService>();
                    //快捷键
                    serviceCollection.Add<KeyboardShortcutsService>();
                    //预提醒
                    serviceCollection.Add<PreAlertService>();
                    //番茄时钟
                    serviceCollection.Add<TomatoService>();

                    WindowManager.serviceCollection = serviceCollection;
                    //初始化所有服务
                    serviceCollection.Initialize();
                    OnServiceInitialized?.Invoke();
                }
                catch (Exception ex)
                {
                    LogHelper.Error("服务初始化失败: " + ex.ToString());
                    MessageBox.Show("程序启动失败，请查看 Log 目录下的错误日志。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
        }

        /// <summary>
        /// 非 UI 线程未处理异常
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    LogHelper.Error("UnhandledException (非UI线程): " + ex.ToString());
                }
                else
                {
                    LogHelper.Error("UnhandledException (非UI线程): " + (e.ExceptionObject?.ToString() ?? "未知异常"));
                }
            }
            catch
            {
                // 日志写入本身失败时静默处理，避免递归崩溃
            }
        }

        /// <summary>
        /// Task 中未观察到的异常
        /// </summary>
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                LogHelper.Error("UnobservedTaskException: " + e.Exception.ToString());
                e.SetObserved(); // 防止进程崩溃
            }
            catch
            {
                // 日志写入本身失败时静默处理
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                LogHelper.Error(e.Exception.ToString());
            }
            catch
            {
                // 日志写入失败时静默处理
            }
            e.Handled = true;
            Shutdown();
            try
            {
                string exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "ProjectEyeBug.exe");
                ProcessHelper.Run(exePath, new string[] { "" });
            }
            catch
            {
                // Bug 报告程序启动失败时静默处理
            }
        }

        #region 获取当前程序是否已运行
        /// <summary>
        /// 获取当前程序是否已运行
        /// </summary>
        private bool IsRuned()
        {
            bool ret;
            mutex = new System.Threading.Mutex(true, "projecteye", out ret);
            if (!ret)
            {
#if !DEBUG
                return true;

#endif
            }
            return false;
        }
        #endregion

    }
}
