using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace NekoGirl_winui3.Animations;

/// <summary>
/// 卡片进入动画 - 从左右弹出
/// </summary>
public static class CardAnimations
{
    /// <summary>
    /// 播放所有卡片的进入动画
    /// </summary>
    public static async Task PlayEntranceAsync(
        Microsoft.UI.Dispatching.DispatcherQueue dispatcher,
        Border imageDisplayBorder,
        Border artistInfoCard,
        Border browseControlCard,
        Border saveManageCard)
    {
        var tcs = new TaskCompletionSource();
        int completed = 0;
        const int total = 4;

        void OnCompleted()
        {
            if (Interlocked.Increment(ref completed) >= total)
                tcs.SetResult();
        }

        await dispatcher.EnqueueAsync(() =>
        {
            AnimateEntrance(imageDisplayBorder, fromLeft: true, delayMs: 0, OnCompleted);
            AnimateEntrance(artistInfoCard, fromLeft: false, delayMs: 100, OnCompleted);
            AnimateEntrance(browseControlCard, fromLeft: false, delayMs: 200, OnCompleted);
            AnimateEntrance(saveManageCard, fromLeft: false, delayMs: 300, OnCompleted);
        });

        await tcs.Task;
    }

    private static void AnimateEntrance(Border card, bool fromLeft, int delayMs, Action? onCompleted)
    {
        var transform = new TranslateTransform { X = fromLeft ? -100 : 100 };
        card.RenderTransform = transform;
        card.Opacity = 0;

        var storyboard = new Storyboard();

        // 位移动画
        var xAnim = new DoubleAnimation
        {
            From = fromLeft ? -100 : 100,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(500)),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };
        Storyboard.SetTarget(xAnim, transform);
        Storyboard.SetTargetProperty(xAnim, "X");
        storyboard.Children.Add(xAnim);

        // 淡入动画
        var opacityAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        Storyboard.SetTarget(opacityAnim, card);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        storyboard.Children.Add(opacityAnim);

        if (onCompleted != null)
            storyboard.Completed += (s, e) => onCompleted();

        storyboard.Begin();
    }
}
