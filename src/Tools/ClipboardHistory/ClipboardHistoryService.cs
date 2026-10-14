using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kutu.Core.Diagnostics;
using Kutu.Core.Models;
using Kutu.Core.Settings;
using Kutu.Sdk.Hosting;

namespace Kutu.Tools.ClipboardHistory;

public sealed class ClipboardHistoryService : IClipboardHistory
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly List<ClipItem> _items = [];
    private readonly string _indexPath;
    private readonly string _imageDir;
    private int _suppress;
    private HwndSource? _source;

    public ClipboardHistoryService()
    {
        var root = SettingsService.AppDirectory;
        _indexPath = Path.Combine(root, "clipboard.json");
        _imageDir = Path.Combine(root, "clipboard");
        Load();
    }

    public bool Enabled { get; set; } = true;

    public IReadOnlyList<ClipItem> Items => _items;

    public event EventHandler? Changed;

    public void Start(Window window)
    {
        try
        {
            _source = new HwndSource(new HwndSourceParameters("Kutu.Clipboard")
            {
                ParentWindow = new IntPtr(-3),
                WindowStyle = 0
            });
        }
        catch (Exception ex)
        {
            AppLog.Write("clipboard message window: " + ex.Message);
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        }

        _source?.AddHook(WndProc);
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || !AddClipboardFormatListener(handle))
            AppLog.Write("AddClipboardFormatListener failed error=" + Marshal.GetLastWin32Error());
    }

    public void Suppress(TimeSpan duration)
    {
        Interlocked.Increment(ref _suppress);
        _ = Task.Run(async () =>
        {
            await Task.Delay(duration);
            Interlocked.Decrement(ref _suppress);
        });
    }

    public void Remove(string id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is null)
            return;

        _items.Remove(item);
        TryDelete(item.ImagePath);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePin(string id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is null)
            return;

        item.Pinned = !item.Pinned;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearUnpinned()
    {
        foreach (var item in _items.Where(x => !x.Pinned).ToList())
        {
            _items.Remove(item);
            TryDelete(item.ImagePath);
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Copy(ClipItem item)
    {
        Suppress(TimeSpan.FromMilliseconds(500));
        if (item.Kind == "image" && item.ImagePath is not null && File.Exists(item.ImagePath))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(item.ImagePath);
            image.EndInit();
            image.Freeze();
            Clipboard.SetImage(image);
            return;
        }

        Clipboard.SetText(item.Text ?? "");
    }

    public void Remember(string text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text))
            return;

        AddText(text.Length > 200_000 ? text[..200_000] : text);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ReadSoon));
        return IntPtr.Zero;
    }

    private async void ReadSoon()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (!Enabled || Volatile.Read(ref _suppress) > 0)
                return;

            try
            {
                if (TryReadText(out var text))
                {
                    AddText(text);
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    var image = Clipboard.GetImage();
                    if (image is not null)
                        AddImage(image);
                }

                return;
            }
            catch (Exception ex)
            {
                if (attempt == 5)
                {
                    AppLog.Write("clipboard read: " + ex.Message);
                    return;
                }

                await Task.Delay(40);
            }
        }
    }

    private static bool TryReadText(out string text)
    {
        text = "";
        if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            text = Clipboard.GetText(TextDataFormat.UnicodeText);
        if (string.IsNullOrWhiteSpace(text) && Clipboard.ContainsText(TextDataFormat.Text))
            text = Clipboard.GetText(TextDataFormat.Text);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Length > 200_000 ? text[..200_000] : text;
        return true;
    }

    private void AddText(string text)
    {
        var existing = _items.FirstOrDefault(x => x.Kind == "text" && x.Text == text);
        if (existing is not null)
        {
            existing.CreatedAt = DateTimeOffset.Now;
            _items.Remove(existing);
            _items.Insert(0, existing);
        }
        else
        {
            _items.Insert(0, new ClipItem { Kind = "text", Text = text });
        }

        Trim();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddImage(BitmapSource image)
    {
        Directory.CreateDirectory(_imageDir);
        var item = new ClipItem { Kind = "image", Text = "PNG" };
        var path = Path.Combine(_imageDir, item.Id + ".png");
        using (var stream = File.Create(path))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        item.ImagePath = path;
        _items.Insert(0, item);
        Trim();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Trim()
    {
        var limit = 50;
        var unpinned = _items.Where(x => !x.Pinned).Skip(limit).ToList();
        foreach (var item in unpinned)
        {
            _items.Remove(item);
            TryDelete(item.ImagePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_indexPath))
                return;

            var items = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(_indexPath));
            if (items is null)
                return;

            foreach (var item in items
                         .Where(x => x.Kind != "image" || (x.ImagePath is not null && File.Exists(x.ImagePath)))
                         .OrderByDescending(x => x.CreatedAt))
                _items.Add(item);
        }
        catch
        {
            // Ignore a corrupt history file.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_indexPath)!);
            File.WriteAllText(_indexPath, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // History is best-effort.
        }
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore locked files.
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
}
