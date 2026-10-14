using System.Windows;

namespace Kutu.Sdk.Hosting;

public interface IKutuTool
{
    string Id { get; }

    string TitleKey { get; }

    string DescriptionKey { get; }

    /// <summary>WPF-UI SymbolRegular name, for example Image24.</summary>
    string Symbol { get; }

    IReadOnlyList<string> Aliases { get; }

    int Order { get; }

    UIElement CreateView(IKutuHost host);
}
