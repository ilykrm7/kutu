using System.Windows;
using Kutu.Sdk.Hosting;
using Wpf.Ui.Controls;

namespace Kutu.Tools.ImageConverter;

public sealed class ImageConverterTool : IKutuTool
{
    public string Id => "image-converter";

    public string TitleKey => "Tool_Image_Title";

    public string DescriptionKey => "Tool_Image_Desc";

    public string Symbol => "Image24";

    public IReadOnlyList<string> Aliases { get; } = ["img", "image", "resim", "gorsel", "görsel", "dönüştür", "donustur", "convert", "png", "jpg", "webp", "heic", "ico"];

    public int Order => 10;

    public UIElement CreateView(IKutuHost host) => new ImageConverterView(host);
}
