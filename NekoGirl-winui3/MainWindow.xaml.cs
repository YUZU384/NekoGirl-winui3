using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using NekoGirl_winui3.Services;
using System.Diagnostics;
using System.Text.Json;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;

namespace NekoGirl_winui3;

public sealed partial class MainWindow : Window
{
    private readonly GetImageService _imageService = new();
    private int _currentIndex = -1;
    private string _saveDirectory = "";
    private AppWindow? _appWindow;
    private readonly string _configPath;
    private bool _isNavigating;
    private readonly DispatcherTimer _statusTimer;

    public MainWindow()
    {
        InitializeComponent();

        // 配置文件路径
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NekoGirl",
            "config.json");

        // 初始化状态定时器
        _statusTimer = new DispatcherTimer();
        _statusTimer.Tick += StatusTimer_Tick;

        // 初始化窗口
        InitializeWindow();

        // 加载配置
        LoadConfig();

        // 窗口启动时预加载图片
        _ = InitializeAsync();
    }

    private void InitializeWindow()
    {
        // 获取 AppWindow
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null) return;

        // 设置窗口大小
        SetWindowSize(704, 747);

        // 设置窗口居中
        CenterWindow();

        // 配置自定义标题栏
        SetupCustomTitleBar();

        // 设置窗口最小尺寸
        SetMinWindowSize();

        // 注册键盘事件
        Content.KeyDown += MainWindow_KeyDown;
    }

    private void SetupCustomTitleBar()
    {
        if (_appWindow == null || !AppWindowTitleBar.IsCustomizationSupported()) return;

        var titleBar = _appWindow.TitleBar;

        // 将内容扩展到标题栏
        titleBar.ExtendsContentIntoTitleBar = true;

        // 设置标题栏按钮的背景颜色（透明）
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 230, 230, 230);

        // 设置标题栏按钮的前景色
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonInactiveForegroundColor = Colors.White;
        titleBar.ButtonHoverForegroundColor = Colors.Black;
        titleBar.ButtonPressedForegroundColor = Colors.Black;

        // 设置标题栏高度区域（拖动区域）
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // 设置可拖动区域
        SetTitleBar(TitleBarGrid);
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.SaveDirectory) && Directory.Exists(config.SaveDirectory))
                {
                    _saveDirectory = config.SaveDirectory;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[配置加载失败] {ex.Message}");
        }

        // 如果没有有效配置，使用默认路径
        if (string.IsNullOrEmpty(_saveDirectory))
        {
            _saveDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "neko");
            Directory.CreateDirectory(_saveDirectory);
        }

        SavePathText.Text = $"保存位置: {_saveDirectory}";
    }

    private void SaveConfig()
    {
        try
        {
            var configDir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var config = new AppConfig { SaveDirectory = _saveDirectory };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[配置保存失败] {ex.Message}");
        }
    }

    private void SetWindowSize(int width, int height)
    {
        _appWindow?.Resize(new SizeInt32(width, height));
    }

    private void SetMinWindowSize()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 704;
            presenter.PreferredMinimumHeight = 747;
        }
    }

    private void CenterWindow()
    {
        try
        {
            if (_appWindow == null) return;

            // 获取屏幕工作区
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            // 计算居中位置
            int windowWidth = 700;
            int windowHeight = 730;
            int x = (workArea.Width - windowWidth) / 2;
            int y = (workArea.Height - windowHeight) / 2;

            // 确保窗口不会超出屏幕边界
            x = Math.Max(workArea.X, Math.Min(x, workArea.X + workArea.Width - windowWidth));
            y = Math.Max(workArea.Y, Math.Min(y, workArea.Y + workArea.Height - windowHeight));

            // 移动窗口
            _appWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[窗口居中失败] {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        ShowStatus("正在初始化，请稍候喵~");
        try
        {
            await _imageService.GetNextSetAsync();
            ShowStatus("初始化完成！点击「下一只」开始浏览喵", 2000);
        }
        catch (Exception ex)
        {
            ShowStatus($"初始化失败: {ex.Message}", 3000);
        }
    }

    private async void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isNavigating || _currentIndex <= 0) return;
        await NavigateToImageAsync(_currentIndex - 1);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;

        // 快看完时预加载
        if (_currentIndex >= _imageService.ImageCount - 2)
        {
            _ = PreloadNextSetAsync();
        }

        await NavigateToImageAsync(_currentIndex + 1);
    }

    private async Task PreloadNextSetAsync()
    {
        try
        {
            await _imageService.GetNextSetAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[预加载失败] {ex.Message}");
        }
    }

    private async Task NavigateToImageAsync(int targetIndex)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        SetButtonsEnabled(false);

        try
        {
            Debug.WriteLine($"导航: 当前索引 {_currentIndex} -> 目标索引 {targetIndex}");

            if (await LoadImageAsync(targetIndex))
            {
                _currentIndex = targetIndex;
            }
        }
        finally
        {
            UpdateButtons();
            SetButtonsEnabled(true);
            _isNavigating = false;
        }
    }

    private async Task<bool> LoadImageAsync(int index)
    {
        ShowStatus("正在加载图像，请等待喵......");
        PlaceholderText.Visibility = Visibility.Collapsed;
        LoadingRing.Visibility = Visibility.Visible;
        MainImage.Visibility = Visibility.Collapsed;

        try
        {
            var bitmap = await _imageService.GetImageAsync(index);

            LoadingRing.Visibility = Visibility.Collapsed;

            if (bitmap == null)
            {
                ShowStatus("图片加载失败，请重试喵~", 2000);
                return false;
            }

            MainImage.Source = bitmap;
            MainImage.Visibility = Visibility.Visible;

            // 更新画师信息
            UpdateArtistInfo(index);

            HideStatus();
            return true;
        }
        catch (Exception ex)
        {
            LoadingRing.Visibility = Visibility.Collapsed;
            ShowStatus($"加载错误: {ex.Message}", 3000);
            return false;
        }
    }

    private void UpdateArtistInfo(int index)
    {
        var (artistName, artistLink) = _imageService.GetArtistInfo(index);

        if (string.IsNullOrEmpty(artistName))
        {
            ArtistText.Text = "暂无信息";
            ArtistLink.Visibility = Visibility.Collapsed;
            ArtistText.Visibility = Visibility.Visible;
        }
        else
        {
            ArtistText.Text = "";
            ArtistText.Visibility = Visibility.Collapsed;
            ArtistLink.Content = $"画师: {artistName}";
            ArtistLink.NavigateUri = new Uri(artistLink);
            ArtistLink.Visibility = Visibility.Visible;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainImage.Source is not BitmapImage || _currentIndex < 0) return;

        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"neko_{timestamp}.png";
            string filePath = Path.Combine(_saveDirectory, fileName);

            // 直接从缓存获取图片字节保存，无需重新下载
            byte[]? imageBytes = _imageService.GetImageBytes(_currentIndex);
            if (imageBytes != null && imageBytes.Length > 0)
            {
                await File.WriteAllBytesAsync(filePath, imageBytes);
                ShowStatus($"图片已保存: {fileName}", 2000);
            }
            else
            {
                ShowStatus("图片数据未找到", 2000);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"保存失败: {ex.Message}", 3000);
        }
    }

    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        };
        folderPicker.FileTypeFilter.Add("*");

        // 获取窗口句柄
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _saveDirectory = folder.Path;
            SavePathText.Text = $"保存位置: {_saveDirectory}";
            SaveConfig();
            ShowStatus("保存目录已更改", 1500);
        }
    }

    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(_saveDirectory);
            await Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception ex)
        {
            ShowStatus($"无法打开目录: {ex.Message}", 3000);
        }
    }

    private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                PrevButton_Click(sender, e);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Right:
            case Windows.System.VirtualKey.Space:
                NextButton_Click(sender, e);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.S:
                if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) == Windows.UI.Core.CoreVirtualKeyStates.Down)
                {
                    SaveButton_Click(sender, e);
                    e.Handled = true;
                }
                break;
        }
    }

    private void UpdateButtons()
    {
        PrevButton.IsEnabled = _currentIndex > 0;
        SaveButton.IsEnabled = MainImage.Source != null;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        PrevButton.IsEnabled = enabled && _currentIndex > 0;
        NextButton.IsEnabled = enabled;
    }

    private void ShowStatus(string message, int autoHideDelayMs = 0)
    {
        _statusTimer.Stop();
        StatusText.Text = message;

        // 停止之前的动画
        StatusHideStoryboard?.Stop();

        // 重置状态 - 从下方滑入
        StatusTransform.X = 0;
        StatusTransform.Y = 20;
        StatusBorder.Opacity = 0;
        StatusBorder.Visibility = Visibility.Visible;

        // 播放显示动画
        StatusShowStoryboard?.Begin();

        // 设置自动隐藏
        if (autoHideDelayMs > 0)
        {
            _statusTimer.Interval = TimeSpan.FromMilliseconds(autoHideDelayMs);
            _statusTimer.Start();
        }
    }

    private void StatusTimer_Tick(object? sender, object e)
    {
        _statusTimer.Stop();
        HideStatus();
    }

    private void HideStatus()
    {
        // 停止之前的动画
        StatusShowStoryboard?.Stop();

        // 播放隐藏动画
        StatusHideStoryboard?.Begin();
    }

    private void StatusHideStoryboard_Completed(object? sender, object e)
    {
        StatusBorder.Visibility = Visibility.Collapsed;
        StatusTransform.X = 0;
        StatusTransform.Y = 20;
        StatusBorder.Opacity = 0;
    }
}

// 配置类
public class AppConfig
{
    public string SaveDirectory { get; set; } = "";
}
