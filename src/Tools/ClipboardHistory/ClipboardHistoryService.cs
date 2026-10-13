using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
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
        var hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
        AddClipboardFormatListener(hwnd);
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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
            Capture();
        return IntPtr.Zero;
    }

    private void Capture()
    {
        if (!Enabled || Volatile.Read(ref _suppress) > 0)
            return;

        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                    AddText(text.Length > 200_000 ? text[..200_000] : text);
                return;
            }

            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image is not null)
                    AddImage(image);
            }
        }
        catch
        {
            // Another app owns the clipboard for a moment.
        }
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

            foreach (var item in items.Where(x => x.Kind != "image" || (x.ImagePath is not null && File.Exists(x.ImagePath))))
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

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
}
