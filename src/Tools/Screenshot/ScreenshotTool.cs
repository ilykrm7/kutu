using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Hotkeys;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;

namespace Kutu.Tools.Screenshot;

public sealed class ScreenshotTool : IKutuTool
{
    public string Id => "screenshot";

    public string TitleKey => "Tool_Shot_Title";

    public string DescriptionKey => "Tool_Shot_Desc";

    public string Symbol => "Camera24";

    public IReadOnlyList<string> Aliases { get; } = ["ss", "shot", "ekran", "screenshot", "capture", "görüntü", "goruntu"];

    public int Order => 30;

    public UIElement CreateView(IKutuHost host) => new ScreenshotView(host);
}

public sealed class ScreenshotView : UserControl
{
    public ScreenshotView(IKutuHost host)
    {
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Shot_Hint")));
        var gesture = ToolHotkeyMap.Get(host.Settings, "screenshot");
        if (!string.IsNullOrWhiteSpace(gesture))
            stack.Children.Add(Chrome.Hint(string.Format(L.Get("Dash_Hint"), gesture)));
        stack.Children.Add(Chrome.Primary(L.Get("Action_TakeShot"), SymbolRegular.Camera24, (_, _) => host.StartScreenshot()));
        Content = stack;
    }
}

public static class ScreenshotController
{
    public static async Task StartAsync(IKutuHost host)
    {
        host.HideForCapture();
        try
        {
            await Task.Delay(180);
            var screen = ScreenCapture.Capture();
            var studio = new CaptureStudio(screen, host);
            studio.ShowDialog();
        }
        finally
        {
            host.RestoreAfterCapture();
        }
    }
}
