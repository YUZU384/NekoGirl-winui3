using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace NekoGirl_winui3.Animations;

/// <summary>
/// 图片动画
/// </summary>
public static class ImageAnimations
{
    /// <summary>
    /// 图片淡入动画
    /// </summary>
    public static void FadeIn(Image image, int durationMs = 300)
    {
        image.Opacity = 0;

        var storyboard = new Storyboard();

        var opacityAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs))
        };
        Storyboard.SetTarget(opacityAnim, image);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        storyboard.Children.Add(opacityAnim);

        storyboard.Begin();
    }
}
