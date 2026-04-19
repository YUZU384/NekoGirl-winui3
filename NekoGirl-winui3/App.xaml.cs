using Microsoft.UI.Xaml;

namespace NekoGirl_winui3;

/// <summary>
/// 应用程序入口
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
