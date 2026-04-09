using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using NekoGirl_winui3.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;

namespace NekoGirl_winui3;

/// <summary>
/// 提示卡片管理器 - 支持多卡片并发
/// </summary>
public class ToastNotificationManager
{
    private readonly Panel _container;
    private readonly List<ToastCard> _activeCards = new();
    private readonly object _lock = new();
    private int _cardIdCounter = 0;

    public ToastNotificationManager(Panel container)
    {
        _container = container;
    }

    /// <summary>
    /// 显示提示卡片
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="durationMs">显示时长，0表示不自动消失</param>
    /// <returns>卡片ID，可用于手动隐藏</returns>
    public int Show(string message, int durationMs = 2000)
    {
        var cardId = Interlocked.Increment(ref _cardIdCounter);

        _ = Task.Run(async () =>
        {
            var card = new ToastCard(cardId, message, _container, this);

            lock (_lock)
            {
                _activeCards.Add(card);
            }

            await card.ShowAsync();

            if (durationMs > 0)
            {
                await Task.Delay(durationMs);
                await card.HideAsync();

                lock (_lock)
                {
                    _activeCards.Remove(card);
                }
            }
        });

        return cardId;
    }

    /// <summary>
    /// 立即隐藏包含指定消息的卡片
    /// </summary>
    public void Hide(string message)
    {
        lock (_lock)
        {
            var cardsToHide = _activeCards.Where(c => c.Message == message).ToList();
            foreach (var card in cardsToHide)
            {
                _ = Task.Run(async () =>
                {
                    await card.HideAsync();
                    lock (_lock)
                    {
                        _activeCards.Remove(card);
                    }
                });
            }
        }
    }

    /// <summary>
    /// 获取活动卡片数量
    /// </summary>
    public int ActiveCardCount
    {
        get
        {
            lock (_lock)
            {
                return _activeCards.Count;
            }
        }
    }
}

/// <summary>
/// 单个提示卡片 - 使用高性能Storyboard动画
/// </summary>
public class ToastCard
{
    private readonly int _id;
    private readonly Panel _container;
    private readonly ToastNotificationManager _manager;
    private Border? _border;
    private TextBlock? _textBlock;
    private Storyboard? _slideInStoryboard;
    private Storyboard? _slideOutStoryboard;

    public string Message { get; }

    public ToastCard(int id, string message, Panel container, ToastNotificationManager manager)
    {
        _id = id;
        Message = message;
        _container = container;
        _manager = manager;
    }

    /// <summary>
    /// 显示卡片 - 从右边滑入
    /// </summary>
    public async Task ShowAsync()
    {
        var tcs = new TaskCompletionSource();

        await _container.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                // 创建卡片UI
                _border = new Border
                {
                    Background = Application.Current.Resources["NekoPinkLightBrush"] as Brush,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 8, 0, 0),
                    Opacity = 0
                };

                _textBlock = new TextBlock
                {
                    Text = Message,
                    FontSize = 13,
                    Foreground = Application.Current.Resources["NekoPinkDarkBrush"] as Brush,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                _border.Child = _textBlock;

                // 设置初始位置
                var transform = new TranslateTransform { X = 120 };
                _border.RenderTransform = transform;

                // 添加到容器顶部
                _container.Children.Insert(0, _border);

                // 创建高性能Storyboard动画
                _slideInStoryboard = CreateSlideInStoryboard(_border, transform);

                // 监听动画完成
                _slideInStoryboard.Completed += (s, e) =>
                {
                    tcs.SetResult();
                };

                // 开始动画
                _border.Opacity = 1;
                _slideInStoryboard.Begin();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        await tcs.Task;
    }

    /// <summary>
    /// 隐藏卡片 - 向下滑出并淡出
    /// </summary>
    public async Task HideAsync()
    {
        if (_border == null) return;

        var tcs = new TaskCompletionSource();

        await _border.DispatcherQueue.EnqueueAsync(() =>
        {
            try
            {
                var transform = _border.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    _border.RenderTransform = transform;
                }

                _slideOutStoryboard = CreateSlideOutStoryboard(_border, transform);

                _slideOutStoryboard.Completed += (s, e) =>
                {
                    if (_container.Children.Contains(_border))
                    {
                        _container.Children.Remove(_border);
                    }
                    tcs.SetResult();
                };

                _slideOutStoryboard.Begin();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        await tcs.Task;
    }

    /// <summary>
    /// 创建滑入Storyboard - 高性能硬件加速动画
    /// </summary>
    private Storyboard CreateSlideInStoryboard(Border border, TranslateTransform transform)
    {
        var storyboard = new Storyboard();

        // X轴位移动画
        var xAnimation = new DoubleAnimation
        {
            From = 120,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(xAnimation, transform);
        Storyboard.SetTargetProperty(xAnimation, "X");
        storyboard.Children.Add(xAnimation);

        return storyboard;
    }

    /// <summary>
    /// 创建滑出Storyboard - 高性能硬件加速动画
    /// </summary>
    private Storyboard CreateSlideOutStoryboard(Border border, TranslateTransform transform)
    {
        var storyboard = new Storyboard();

        // Y轴位移动画
        var yAnimation = new DoubleAnimation
        {
            To = 80,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(yAnimation, transform);
        Storyboard.SetTargetProperty(yAnimation, "Y");
        storyboard.Children.Add(yAnimation);

        // 淡出动画
        var opacityAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(opacityAnimation, border);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        return storyboard;
    }
}

/// <summary>
/// DispatcherQueue 扩展方法
/// </summary>
public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Action action)
    {
        var tcs = new TaskCompletionSource();
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        dispatcher.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}

/// <summary>
/// MainWindow类 - 应用程序主窗口
/// </summary>
public sealed partial class MainWindow : Window
{
    // ============================================
    // 服务与数据字段
    // ============================================
    private readonly GetImageService _imageService = new();
    private int _currentIndex = -1;
    private string _saveDirectory = "";
    private AppWindow? _appWindow;
    private readonly string _configPath;
    private bool _isNavigating;

    // ============================================
    // 动画相关字段
    // ============================================
    private ToastNotificationManager? _toastManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NekoGirl",
            "config.json");

        InitializeWindow();
        LoadConfig();

        _ = InitializeAsync();
    }

    // ============================================
    // 窗口初始化
    // ============================================

    private void InitializeWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null) return;

        SetWindowSize(714, 757);
        CenterWindow();
        SetupCustomTitleBar();
        SetMinWindowSize();
        Content.KeyDown += MainWindow_KeyDown;
    }

    private void SetupCustomTitleBar()
    {
        if (_appWindow == null || !AppWindowTitleBar.IsCustomizationSupported()) return;

        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 230, 230, 230);
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonInactiveForegroundColor = Colors.White;
        titleBar.ButtonHoverForegroundColor = Colors.Black;
        titleBar.ButtonPressedForegroundColor = Colors.Black;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SetTitleBar(TitleBarGrid);
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize(json, AppConfigContext.Default.AppConfig);
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
            var json = JsonSerializer.Serialize(config, AppConfigContext.Default.AppConfig);
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
            presenter.PreferredMinimumWidth = 714;
            presenter.PreferredMinimumHeight = 757;
        }
    }

    private void CenterWindow()
    {
        try
        {
            if (_appWindow == null) return;

            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            int windowWidth = 700;
            int windowHeight = 730;
            int x = (workArea.Width - windowWidth) / 2;
            int y = (workArea.Height - windowHeight) / 2;

            x = Math.Max(workArea.X, Math.Min(x, workArea.X + workArea.Width - windowWidth));
            y = Math.Max(workArea.Y, Math.Min(y, workArea.Y + workArea.Height - windowHeight));

            _appWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[窗口居中失败] {ex.Message}");
        }
    }

    // ============================================
    // 初始化流程
    // ============================================

    private async Task InitializeAsync()
    {
        // 初始化提示卡片管理器
        _toastManager = new ToastNotificationManager(ToastContainer);

        // 播放卡片进入动画
        await PlayCardEntranceAnimationsAsync();

        // 显示初始化提示
        ShowToast("正在初始化，请稍候喵~");

        try
        {
            await _imageService.GetNextSetAsync();
            await Task.Delay(500);
            ShowToast("初始化完成！点击「下一只」开始浏览喵");
        }
        catch (Exception ex)
        {
            ShowToast($"初始化失败: {ex.Message}");
        }
    }

    // ============================================
    // 初始化动画 - 卡片从左右弹出 (使用Storyboard)
    // ============================================

    private async Task PlayCardEntranceAnimationsAsync()
    {
        var tcs = new TaskCompletionSource();
        int completedCount = 0;
        int totalCount = 4;

        void OnAnimationCompleted()
        {
            completedCount++;
            if (completedCount >= totalCount)
            {
                tcs.SetResult();
            }
        }

        // 左侧图片显示区 - 从左边弹出
        await DispatcherQueue.EnqueueAsync(() =>
        {
            AnimateCardEntrance(ImageDisplayBorder, fromLeft: true, delayMs: 0, OnAnimationCompleted);
        });

        // 右侧卡片 - 从右边弹出
        await DispatcherQueue.EnqueueAsync(() =>
        {
            AnimateCardEntrance(ArtistInfoCard, fromLeft: false, delayMs: 100, OnAnimationCompleted);
        });

        await DispatcherQueue.EnqueueAsync(() =>
        {
            AnimateCardEntrance(BrowseControlCard, fromLeft: false, delayMs: 200, OnAnimationCompleted);
        });

        await DispatcherQueue.EnqueueAsync(() =>
        {
            AnimateCardEntrance(SaveManageCard, fromLeft: false, delayMs: 300, OnAnimationCompleted);
        });

        await tcs.Task;
    }

    /// <summary>
    /// 卡片进入动画 - 使用Storyboard硬件加速
    /// </summary>
    private void AnimateCardEntrance(Border card, bool fromLeft, int delayMs, Action onCompleted)
    {
        var transform = new TranslateTransform { X = fromLeft ? -100 : 100 };
        card.RenderTransform = transform;
        card.Opacity = 0;

        var storyboard = new Storyboard();

        // 位移动画
        var xAnimation = new DoubleAnimation
        {
            From = fromLeft ? -100 : 100,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(500)),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };
        Storyboard.SetTarget(xAnimation, transform);
        Storyboard.SetTargetProperty(xAnimation, "X");
        storyboard.Children.Add(xAnimation);

        // 淡入动画
        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        Storyboard.SetTarget(opacityAnimation, card);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        storyboard.Completed += (s, e) => onCompleted?.Invoke();
        storyboard.Begin();
    }

    // ============================================
    // 提示卡片 - 支持多卡片并发
    // ============================================

    private void ShowToast(string message, int durationMs = 2000)
    {
        _toastManager?.Show(message, durationMs);
    }

    private void HideToast(string message)
    {
        _toastManager?.Hide(message);
    }

    // ============================================
    // 图片浏览
    // ============================================

    private async void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isNavigating || _currentIndex <= 0) return;
        await NavigateToImageAsync(_currentIndex - 1);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;

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
        const string loadingMessage = "正在加载图像，请等待喵......";

        // 检查图片是否已缓存
        bool isCached = _imageService.IsImageCached(index);

        // 只有未缓存的图片才显示加载提示
        if (!isCached)
        {
            ShowToast(loadingMessage, 0); // 0表示不自动消失
        }

        PlaceholderText.Visibility = Visibility.Collapsed;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            var bitmap = await _imageService.GetImageAsync(index);

            LoadingRing.Visibility = Visibility.Collapsed;

            if (bitmap == null)
            {
                if (!isCached)
                {
                    HideToast(loadingMessage);
                }
                ShowToast("图片加载失败，请重试喵~");
                return false;
            }

            // 加载成功，如果是未缓存的图片则隐藏加载消息
            if (!isCached)
            {
                HideToast(loadingMessage);
            }

            MainImage.Source = bitmap;
            MainImage.Visibility = Visibility.Visible;

            // 简单的淡入效果 - 使用Storyboard
            AnimateImageFadeIn(MainImage);

            UpdateArtistInfo(index);
            return true;
        }
        catch (Exception ex)
        {
            LoadingRing.Visibility = Visibility.Collapsed;
            if (!isCached)
            {
                HideToast(loadingMessage);
            }
            ShowToast($"加载错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 图片淡入动画 - 使用Storyboard
    /// </summary>
    private void AnimateImageFadeIn(Image image)
    {
        image.Opacity = 0;

        var storyboard = new Storyboard();

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(300))
        };
        Storyboard.SetTarget(opacityAnimation, image);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        storyboard.Begin();
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

    // ============================================
    // 按钮事件
    // ============================================

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainImage.Source is not BitmapImage || _currentIndex < 0) return;

        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"neko_{timestamp}.png";
            string filePath = Path.Combine(_saveDirectory, fileName);

            byte[]? imageBytes = _imageService.GetImageBytes(_currentIndex);
            if (imageBytes != null && imageBytes.Length > 0)
            {
                await File.WriteAllBytesAsync(filePath, imageBytes);
                ShowToast($"图片已保存: {fileName}");
            }
            else
            {
                ShowToast("图片数据未找到");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"保存失败: {ex.Message}");
        }
    }

    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        };
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _saveDirectory = folder.Path;
            SavePathText.Text = $"保存位置: {_saveDirectory}";
            SaveConfig();
            ShowToast("保存目录已更改");
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
            ShowToast($"无法打开目录: {ex.Message}");
        }
    }

    // ============================================
    // 键盘和按钮状态
    // ============================================

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
}

// ============================================
// 配置数据类
// ============================================

public class AppConfig
{
    public string SaveDirectory { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigContext : JsonSerializerContext
{
}
