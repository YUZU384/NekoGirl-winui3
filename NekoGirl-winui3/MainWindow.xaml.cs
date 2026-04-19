using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using NekoGirl_winui3.Animations;
using NekoGirl_winui3.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace NekoGirl_winui3;

/// <summary>
/// 主窗口 - 结合原版简洁和重构版模块化的优化版本
/// </summary>
public sealed partial class MainWindow : Window
{
    // 服务与数据
    private readonly GetImageService _imageService = new();
    private readonly string _configPath;
    private AppWindow? _appWindow;

    private int _currentIndex = -1;
    private string _saveDirectory = "";
    private bool _isNavigating;

    // 动画
    private ToastNotification? _toast;

    public MainWindow()
    {
        InitializeComponent();

        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NekoGirl", "config.json");

        InitializeWindow();
        LoadConfig();
        _ = InitializeAsync();
    }

    #region 窗口初始化

    private void InitializeWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null) return;

        SetWindowSize(714, 757);
        CenterWindow();
        SetupTitleBar();
        SetMinWindowSize();
        Content.KeyDown += OnKeyDown;
    }

    private void SetupTitleBar()
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

    #endregion

    #region 配置管理

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize(json, AppConfigContext.Default.AppConfig);
                if (config?.SaveDirectory is not null && Directory.Exists(config.SaveDirectory))
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
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "neko");
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

    #endregion

    #region 初始化

    private async Task InitializeAsync()
    {
        _toast = new ToastNotification(ToastContainer);

        await CardAnimations.PlayEntranceAsync(
            DispatcherQueue,
            ImageDisplayBorder,
            ArtistInfoCard,
            BrowseControlCard,
            SaveManageCard);

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

    #endregion

    #region Toast 提示

    private void ShowToast(string message, int durationMs = 2000)
    {
        _toast?.Show(message, durationMs);
    }

    private void HideToast(string message)
    {
        _toast?.Hide(message);
    }

    #endregion

    #region 图片浏览

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
        const string loadingMsg = "正在加载图像，请等待喵......";
        bool isCached = _imageService.IsImageCached(index);

        if (!isCached)
            ShowToast(loadingMsg, 0);

        PlaceholderText.Visibility = Visibility.Collapsed;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            var bitmap = await _imageService.GetImageAsync(index);
            LoadingRing.Visibility = Visibility.Collapsed;

            if (bitmap == null)
            {
                if (!isCached) HideToast(loadingMsg);
                ShowToast("图片加载失败，请重试喵~");
                return false;
            }

            if (!isCached) HideToast(loadingMsg);

            MainImage.Source = bitmap;
            MainImage.Visibility = Visibility.Visible;
            ImageAnimations.FadeIn(MainImage);

            UpdateArtistInfo(index);
            return true;
        }
        catch (Exception ex)
        {
            LoadingRing.Visibility = Visibility.Collapsed;
            if (!isCached) HideToast(loadingMsg);
            ShowToast($"加载错误: {ex.Message}");
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

    #endregion

    #region 按钮事件

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

    #endregion

    #region 键盘和按钮状态

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                PrevButton_Click(sender, e);
                e.Handled = true;
                break;
            case VirtualKey.Right:
            case VirtualKey.Space:
                NextButton_Click(sender, e);
                e.Handled = true;
                break;
            case VirtualKey.S:
                if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) == CoreVirtualKeyStates.Down)
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

    #endregion
}

// 配置数据
public class AppConfig
{
    public string SaveDirectory { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigContext : JsonSerializerContext
{
}
