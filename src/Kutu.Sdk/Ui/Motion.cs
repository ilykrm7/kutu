using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Kutu.Sdk.Ui;

public static class Motion
{
    public static void Enter(FrameworkElement element, int delayMs = 0, double distance = 10)
    {
        element.Opacity = 0;
        var slide = new TranslateTransform(0, distance);
        element.RenderTransform = slide;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        };
        var move = new DoubleAnimation(distance, 0, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        slide.BeginAnimation(TranslateTransform.YProperty, move);
    }

    public static void HoverLift(FrameworkElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var move = new TranslateTransform();
        element.RenderTransform = move;
        var shadow = new DropShadowEffect
        {
            BlurRadius = 0,
            ShadowDepth = 0,
            Opacity = 0,
            Color = Colors.Black
        };
        element.Effect = shadow;
        element.MouseEnter += (_, _) =>
        {
            Animate(move, TranslateTransform.YProperty, -2, 180);
            Animate(shadow, DropShadowEffect.BlurRadiusProperty, 22, 180);
            Animate(shadow, DropShadowEffect.OpacityProperty, 0.35, 180);
        };
        element.MouseLeave += (_, _) =>
        {
            Animate(move, TranslateTransform.YProperty, 0, 180);
            Animate(shadow, DropShadowEffect.BlurRadiusProperty, 0, 160);
            Animate(shadow, DropShadowEffect.OpacityProperty, 0, 160);
        };
    }

    public static void Pressable(FrameworkElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1);
        var move = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, move } };
        element.MouseEnter += (_, _) => Animate(move, TranslateTransform.YProperty, -1, 160);
        element.MouseLeave += (_, _) =>
        {
            Animate(move, TranslateTransform.YProperty, 0, 160);
            Animate(scale, ScaleTransform.ScaleXProperty, 1, 120);
            Animate(scale, ScaleTransform.ScaleYProperty, 1, 120);
        };
        element.PreviewMouseLeftButtonDown += (_, _) =>
        {
            Animate(scale, ScaleTransform.ScaleXProperty, 0.97, 90);
            Animate(scale, ScaleTransform.ScaleYProperty, 0.97, 90);
        };
        element.PreviewMouseLeftButtonUp += (_, _) =>
        {
            Animate(scale, ScaleTransform.ScaleXProperty, 1, 120);
            Animate(scale, ScaleTransform.ScaleYProperty, 1, 120);
        };
    }

    public static void Play(FrameworkElement target, double fromY, double toY, double fromOpacity, double toOpacity, double fromScale, double toScale, int ms, Action? completed = null)
    {
        target.RenderTransformOrigin = new Point(0.5, 0.5);
        var group = new TransformGroup();
        var scale = new ScaleTransform(fromScale, fromScale);
        var move = new TranslateTransform(0, fromY);
        group.Children.Add(scale);
        group.Children.Add(move);
        target.RenderTransform = group;
        target.Opacity = fromOpacity;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(fromOpacity, toOpacity, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        var slide = new DoubleAnimation(fromY, toY, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        var grow = new DoubleAnimation(fromScale, toScale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        if (completed is not null)
            fade.Completed += (_, _) => completed();
        target.BeginAnimation(UIElement.OpacityProperty, fade);
        move.BeginAnimation(TranslateTransform.YProperty, slide);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    private static void Animate(Animatable target, DependencyProperty property, double to, int ms)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        target.BeginAnimation(property, animation);
    }
}
