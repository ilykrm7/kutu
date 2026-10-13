using System.Windows.Media.Imaging;
using Kutu.Core.Models;
using Kutu.Core.Settings;

namespace Kutu.Sdk.Hosting;

public interface IKutuHost
{
    AppSettings Settings { get; }

    SettingsService SettingsStore { get; }

    IClipboardHistory Clipboard { get; }

    IReadOnlyList<IKutuTool> Tools { get; }

    Task SaveSettingsAsync(CancellationToken cancellationToken = default);

    void Toast(string message);

    void CopyText(string text);

    void CopyImage(BitmapSource image);

    Task<string?> UploadAsync(byte[] data, string fileName, string contentType, CancellationToken cancellationToken = default);

    void SuppressClipboard(TimeSpan duration);

    void OpenTool(string toolId, object? parameter = null);

    void ShowDashboard();

    void ShowSettings();

    void ShowAndActivate();

    void HideForCapture();

    void RestoreAfterCapture();

    void StartScreenshot();

    void StartColorPicker();

    void ShowClipboardPopup();

    void ShowTranslatePopup();

    void RebindHotkeys();

    void Exit();
}
