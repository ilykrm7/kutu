using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kutu.Core.Settings;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Windows;

namespace Kutu.Tools.Translate;

public static class SelectionTranslator
{
    public static async Task RunAsync(IKutuHost host)
    {
        if (ForegroundElevation.BlocksTyping())
        {
            host.Log("translate skipped: foreground process is elevated");
            ForegroundElevation.NotifyOnce(host);
            return;
        }

        TinyToast? toast = null;
        try
        {
            toast = TinyToast.Show(L.Get("Translate_Working"));
        }
        catch (Exception ex)
        {
            host.Log("translate toast: " + ex.Message);
        }

        IDataObject? backup = null;
        try
        {
            host.SuppressClipboard(TimeSpan.FromMilliseconds(1500));
            Trace(host, "translate scheduled");
            var captured = await KeyInjector.RunAsync(() =>
            {
                var previous = KeyInjector.Snapshot();
                var text = KeyInjector.CaptureSelection();
                return (previous, text);
            });
            backup = captured.previous;
            if (string.IsNullOrWhiteSpace(captured.text))
            {
                host.Log("translate: selection copy was empty (" + SelectionGrab.LastDiagnostic + ")");
                await KeyInjector.RunAsync(() => KeyInjector.Restore(backup));
                backup = null;
                toast?.Set(L.Get("Translate_NoSelection"));
                await Task.Delay(900);
                return;
            }

            var result = await TranslateClient.TranslateAutoAsync(captured.text, host.Settings.DeepLKey, host.Settings.DeepLEndpoint);
            var translated = string.IsNullOrWhiteSpace(result.Text) ? captured.text : result.Text;
            host.SuppressClipboard(TimeSpan.FromMilliseconds(1500));
            await KeyInjector.RunAsync(() =>
            {
                var typed = KeyInjector.Send(KeyStrokePlan.Unicode(translated));
                Trace(host, $"translate unicode sent={typed.AcceptedEvents} result={typed.AcceptedEvents} err={typed.Win32Error}");
                if (!typed.Ok)
                {
                    host.Log($"translate inject failed events={typed.AcceptedEvents} error={typed.Win32Error}");
                    var pasted = KeyInjector.PasteText(translated);
                    Trace(host, $"translate paste result={pasted.AcceptedEvents} err={pasted.Win32Error}");
                    if (!pasted.Ok)
                        host.Log($"translate paste failed events={pasted.AcceptedEvents} error={pasted.Win32Error}");
                }

                Thread.Sleep(80);
                KeyInjector.Restore(backup);
            });
            backup = null;
        }
        catch (Exception ex)
        {
            if (backup is not null)
            {
                try
                {
                    await KeyInjector.RunAsync(() => KeyInjector.Restore(backup));
                }
                catch
                {
                    // The previous clipboard contents could not be restored.
                }
            }

            host.Log("translate: " + ex);
            var note = ex.Message == "translate-failed" ? L.Get("Translate_Failed") : ex.Message;
            toast?.Set(note.Length > 80 ? note[..80] : note);
            await Task.Delay(1200);
        }
        finally
        {
            toast?.Close();
        }
    }

    private static void Trace(IKutuHost host, string line)
    {
        if (!host.Settings.ExpanderDebug)
            return;

        try
        {
            var dir = SettingsService.AppDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "expander.log"), $"{DateTimeOffset.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch
        {
            // Debug logging must not stop translation.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    private sealed class TinyToast
    {
        private readonly Window _window;
        private readonly TextBlock _text;

        private TinyToast()
        {
            _text = new TextBlock { Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            _window = new Window
            {
                Width = 220,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                ResizeMode = ResizeMode.NoResize,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x10, 0x18, 0x28)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 8, 12, 8),
                    Child = _text
                }
            };
            if (GetCursorPos(out var point))
            {
                _window.Left = point.X + 16;
                _window.Top = point.Y + 18;
            }
        }

        public static TinyToast Show(string text)
        {
            var toast = new TinyToast();
            toast._text.Text = text;
            toast._window.Show();
            return toast;
        }

        public void Set(string text) => _text.Text = text;

        public void Close()
        {
            try
            {
                _window.Close();
            }
            catch
            {
                // The toast may already be closed.
            }
        }
    }
}
