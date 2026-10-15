using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Security;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Kutu.Tools.Password;

public sealed class PasswordTool : IKutuTool
{
    public string Id => "password";

    public string TitleKey => "Tool_Pass_Title";

    public string DescriptionKey => "Tool_Pass_Desc";

    public string Symbol => "Password24";

    public IReadOnlyList<string> Aliases { get; } = ["parola", "şifre", "sifre", "password", "pass"];

    public int Order => 112;

    public UIElement CreateView(IKutuHost host) => new PasswordView(host);
}

public sealed class PasswordView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _length;
    private readonly CheckBox _upper;
    private readonly CheckBox _lower;
    private readonly CheckBox _digits;
    private readonly CheckBox _symbols;
    private readonly CheckBox _ambiguous;
    private readonly TextBox _output;

    public PasswordView(IKutuHost host)
    {
        _host = host;
        var settings = host.Settings;
        _length = Chrome.Input("20");
        _length.Text = settings.PasswordLength.ToString();
        _length.Width = 88;
        _upper = Box(L.Get("Pass_Upper"), settings.PasswordUpper);
        _lower = Box(L.Get("Pass_Lower"), settings.PasswordLower);
        _digits = Box(L.Get("Pass_Digits"), settings.PasswordDigits);
        _symbols = Box(L.Get("Pass_Symbols"), settings.PasswordSymbols);
        _ambiguous = Box(L.Get("Pass_Ambiguous"), settings.PasswordExcludeAmbiguous);
        _output = Chrome.Input(L.Get("Tool_Pass_Title"));
        _output.IsReadOnly = true;
        var checks = new WrapPanel();
        checks.Children.Add(_upper);
        checks.Children.Add(_lower);
        checks.Children.Add(_digits);
        checks.Children.Add(_symbols);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Generate"), SymbolRegular.Password24, (_, _) => Generate()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_output.Text))
            {
                host.CopyText(_output.Text);
                host.Toast(L.Get("Action_Copied"));
            }
        }));
        buttons.Children.Add(Chrome.Secondary(L.Get("Pass_SaveDefault"), SymbolRegular.Save24, async (_, _) => await SaveDefaultsAsync()));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Pass_Desc")));
        stack.Children.Add(Chrome.Label(L.Get("Pass_Length")));
        stack.Children.Add(_length);
        stack.Children.Add(checks);
        stack.Children.Add(_ambiguous);
        stack.Children.Add(buttons);
        stack.Children.Add(_output);
        Content = stack;
        Generate();
    }

    private void Generate()
    {
        _output.Text = PasswordGenerator.Create(Recipe());
    }

    private async Task SaveDefaultsAsync()
    {
        var recipe = Recipe();
        var settings = _host.Settings;
        settings.PasswordLength = recipe.Length;
        settings.PasswordUpper = recipe.Upper;
        settings.PasswordLower = recipe.Lower;
        settings.PasswordDigits = recipe.Digits;
        settings.PasswordSymbols = recipe.Symbols;
        settings.PasswordExcludeAmbiguous = recipe.ExcludeAmbiguous;
        await _host.SaveSettingsAsync();
        _host.Toast(L.Get("Settings_Saved"));
    }

    private PasswordRecipe Recipe()
    {
        if (!int.TryParse(_length.Text.Trim(), out var length))
            length = 20;
        return new PasswordRecipe(length, _upper.IsChecked == true, _lower.IsChecked == true, _digits.IsChecked == true, _symbols.IsChecked == true, _ambiguous.IsChecked == true);
    }

    private static CheckBox Box(string label, bool on) => new()
    {
        Content = label,
        IsChecked = on,
        Foreground = Chrome.Text,
        Margin = new Thickness(0, 6, 16, 6)
    };
}
