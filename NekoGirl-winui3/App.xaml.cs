using Microsoft.UI.Xaml;

namespace NekoGirl_winui3
{
    /// <summary>
    /// App类 - 应用程序入口点
    /// 继承自Microsoft.UI.Xaml.Application，提供应用生命周期管理
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 主窗口实例引用
        /// 类型: Window (WinUI 3窗口基类)
        /// 用途: 保存主窗口引用以便应用生命周期内管理窗口
        /// </summary>
        private Window? _window;

        /// <summary>
        /// 构造函数 - 初始化应用程序实例
        /// 调用链: App() -> InitializeComponent() -> 加载App.xaml中定义的资源
        /// 执行时机: 应用启动时，由Windows App SDK自动调用
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// OnLaunched - 应用启动时调用
        /// 参数: LaunchActivatedEventArgs args - 包含启动参数信息
        /// 执行流程:
        ///   1. 创建MainWindow实例
        ///   2. 调用Activate()激活窗口并显示
        /// 触发时机: 应用启动、从挂起恢复、通过协议/文件关联启动
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
