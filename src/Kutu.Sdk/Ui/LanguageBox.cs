using System.Windows.Controls;
using Kutu.Core.Text;
using Kutu.Sdk.Localization;

namespace Kutu.Sdk.Ui;

public static class LanguageBox
{
    public static ComboBox Create(string? selectedCode, Action<string>? onChanged)
    {
        var box = new ComboBox { MinWidth = 200, Height = 36 };
        foreach (var code in TranslateDirection.Targets)
            box.Items.Add(new ComboBoxItem { Content = Label(code), Tag = code });
        Select(box, selectedCode);
        if (onChanged is null)
            return box;

        box.SelectionChanged += (_, _) =>
        {
            var code = Selected(box);
            if (code.Length > 0)
                onChanged(code);
        };
        return box;
    }

    public static void Select(ComboBox box, string? code)
    {
        var normalized = TranslateDirection.RememberedTarget(code);
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem choice && choice.Tag as string == normalized)
            {
                box.SelectedItem = choice;
                return;
            }
        }
    }

    public static string Selected(ComboBox box) =>
        box.SelectedItem is ComboBoxItem item && item.Tag is string code
            ? TranslateDirection.RememberedTarget(code)
            : "TR";

    public static string Label(string code) => L.Get("Target_" + TranslateDirection.RememberedTarget(code));
}
