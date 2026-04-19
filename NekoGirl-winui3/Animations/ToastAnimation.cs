using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace NekoGirl_winui3.Animations;

/// <summary>
/// Toast通知管理器 - 支持卡片堆叠动画
/// 效果：新卡片从右侧滑入，已有卡片向下滑动让出位置
/// </summary>
public class ToastNotification
{
    private readonly Panel _container;
    private readonly List<ToastCard> _activeCards = new();
    private readonly Lock _lock = new();
    private int _idCounter;

    public ToastNotification(Panel container)
    {
        _container = container;
    }

    /// <summary>
    /// 显示Toast消息
    /// </summary>
    public void Show(string message, int durationMs = 2000)
    {
        var card = new ToastCard(Interlocked.Increment(ref _idCounter), message, _container, this);

        lock (_lock)
        {
            _activeCards.Add(card);
        }

        _ = ShowCardAsync(card, durationMs);
    }

    /// <summary>
    /// 隐藏包含指定消息的所有Toast
    /// </summary>
    public void Hide(string message)
    {
        List<ToastCard> toHide;
        lock (_lock)
        {
            toHide = _activeCards.Where(c => c.Message == message).ToList();
        }

        foreach (var card in toHide)
        {
            _ = card.HideAsync();
        }
    }

    /// <summary>
    /// 获取卡片在列表中的索引（用于计算位置）
    /// </summary>
    internal int GetCardIndex(ToastCard card)
    {
        lock (_lock)
        {
            return _activeCards.IndexOf(card);
        }
    }

    /// <summary>
    /// 获取所有活动卡片（用于动画）
    /// </summary>
    internal List<ToastCard> GetActiveCards()
    {
        lock (_lock)
        {
            return _activeCards.ToList();
        }
    }

    internal void Remove(ToastCard card)
    {
        lock (_lock)
        {
            _activeCards.Remove(card);
        }
    }

    /// <summary>
    /// 显示卡片的完整流程
    /// </summary>
    private async Task ShowCardAsync(ToastCard card, int durationMs)
    {
        // 1. 先创建卡片UI（不可见）
        await card.CreateAsync();

        // 2. 获取当前所有已有卡片（不包括新卡片）
        var existingCards = GetActiveCards().Where(c => c != card).ToList();

        // 3. 同时执行：
        //    - 新卡片从右侧滑入
        //    - 已有卡片向下滑动
        var animationTasks = new List<Task>();

        // 新卡片滑入
        animationTasks.Add(card.SlideInAsync());

        // 已有卡片向下滑动（滑动距离 = 新卡片的高度 + Margin）
        // Margin.Bottom = 8px 是卡片之间的间距
        double slideOffset = card.GetHeight() + 8;
        foreach (var existingCard in existingCards)
        {
            animationTasks.Add(existingCard.SlideDownAsync(slideOffset));
        }

        await Task.WhenAll(animationTasks);

        // 4. 等待显示时间后自动隐藏
        if (durationMs > 0)
        {
            await Task.Delay(durationMs);
            await card.HideAsync();
        }
    }
}

/// <summary>
/// 单个Toast卡片
/// </summary>
public class ToastCard
{
    private readonly int _id;
    private readonly Panel _container;
    private readonly ToastNotification _manager;
    private Border? _border;
    private double _currentYOffset; // 当前Y轴偏移量（用于堆叠）

    public string Message { get; }
    private double _cachedHeight;
    public double GetHeight() => _cachedHeight;

    /// <summary>
    /// 根据字符数计算卡片高度（加定值修正）
    /// </summary>
    private static double CalculateHeight(string message)
    {
        const double lineHeight = 18; // 13px字体行高
        const int charsPerLine = 22;  // 每行大约22个字符
        const double padding = 24;    // Padding 上下各12px
        const double adjust = -40;     // 定值修正（减小高度）

        // 计算需要的行数
        int lineCount = Math.Max(1, (int)Math.Ceiling((double)message.Length / charsPerLine));

        // 总高度 = 行数 * 行高 + padding + 修正值
        return lineCount * lineHeight + padding + adjust;
    }

    public ToastCard(int id, string message, Panel container, ToastNotification manager)
    {
        _id = id;
        Message = message;
        _container = container;
        _manager = manager;
    }

    /// <summary>
    /// 创建卡片UI（初始状态：透明、在右侧外）
    /// </summary>
    public async Task CreateAsync()
    {
        var tcs = new TaskCompletionSource();

        await _container.DispatcherQueue.EnqueueAsync(() =>
        {
            _border = new Border
            {
                Background = Application.Current.Resources["NekoPinkLightBrush"] as Brush,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = 0,
                // 初始位置：右侧外（X=120），Y轴根据索引计算
                RenderTransform = new TranslateTransform { X = 120, Y = 0 }
            };

            var textBlock = new TextBlock
            {
                Text = Message,
                FontSize = 13,
                Foreground = Application.Current.Resources["NekoPinkDarkBrush"] as Brush,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _border.Child = textBlock;

            // 先添加到容器但不显示
            _container.Children.Insert(0, _border);

            // 根据字符数计算高度（更可靠）
            _cachedHeight = CalculateHeight(Message);

            tcs.SetResult();
        });

        await tcs.Task;
    }

    /// <summary>
    /// 从右侧滑入
    /// </summary>
    public async Task SlideInAsync()
    {
        if (_border == null) return;

        var tcs = new TaskCompletionSource();

        await _border.DispatcherQueue.EnqueueAsync(() =>
        {
            var transform = _border.RenderTransform as TranslateTransform;
            if (transform == null) return;

            var storyboard = new Storyboard();

            // X轴：从右侧滑入
            var xAnim = new DoubleAnimation
            {
                From = 120,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(xAnim, transform);
            Storyboard.SetTargetProperty(xAnim, "X");
            storyboard.Children.Add(xAnim);

            // 淡入
            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            Storyboard.SetTarget(opacityAnim, _border);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            storyboard.Completed += (s, e) => tcs.SetResult();

            _border.Opacity = 1;
            storyboard.Begin();
        });

        await tcs.Task;
    }

    /// <summary>
    /// 向下滑动（给新卡片让位置）
    /// </summary>
    public async Task SlideDownAsync(double offset)
    {
        if (_border == null) return;

        var tcs = new TaskCompletionSource();

        await _border.DispatcherQueue.EnqueueAsync(() =>
        {
            // 累加偏移量
            _currentYOffset += offset;

            var transform = _border.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform { X = 0, Y = _currentYOffset };
                _border.RenderTransform = transform;
            }

            var storyboard = new Storyboard();

            var yAnim = new DoubleAnimation
            {
                To = _currentYOffset,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(yAnim, transform);
            Storyboard.SetTargetProperty(yAnim, "Y");
            storyboard.Children.Add(yAnim);

            storyboard.Completed += (s, e) => tcs.SetResult();

            storyboard.Begin();
        });

        await tcs.Task;
    }

    /// <summary>
    /// 隐藏并移除卡片
    /// </summary>
    public async Task HideAsync()
    {
        if (_border == null) return;

        var tcs = new TaskCompletionSource();

        await _border.DispatcherQueue.EnqueueAsync(() =>
        {
            var transform = _border.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                _border.RenderTransform = transform;
            }

            var storyboard = new Storyboard();

            // 向下滑出
            var yAnim = new DoubleAnimation
            {
                To = _currentYOffset + 40,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(yAnim, transform);
            Storyboard.SetTargetProperty(yAnim, "Y");
            storyboard.Children.Add(yAnim);

            // 淡出
            var opacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnim, _border);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            storyboard.Completed += (s, e) =>
            {
                if (_container.Children.Contains(_border))
                    _container.Children.Remove(_border);
                _manager.Remove(this);
                tcs.SetResult();
            };

            storyboard.Begin();
        });

        await tcs.Task;
    }
}
