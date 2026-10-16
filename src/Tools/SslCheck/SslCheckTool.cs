using System.Net.Security;
using Kutu.Core.Network;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.SslCheck;

public sealed class SslCheckTool : IKutuTool
{
    public string Id => "ssl";

    public string TitleKey => "Tool_Ssl_Title";

    public string DescriptionKey => "Tool_Ssl_Desc";

    public string Symbol => "LockClosed24";

    public IReadOnlyList<string> Aliases { get; } = ["ssl", "tls", "sertifika", "cert", "https"];

    public int Order => 76;

    public UIElement CreateView(IKutuHost host) => new SslCheckView(host);
}

public sealed class SslCheckView : UserControl
{
    private readonly TextBox _host;
    private readonly StackPanel _body;

    public SslCheckView(IKutuHost host)
    {
        _host = Chrome.Input("example.com");
        _host.Width = 320;
        _host.Margin = new Thickness(0, 0, 8, 0);
        _body = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_host);
        row.Children.Add(Chrome.Primary(L.Get("Action_Check"), SymbolRegular.LockClosed24, async (_, _) => await CheckAsync(host)));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Ssl_Desc")));
        stack.Children.Add(Chrome.Label(L.Get("Ssl_Host")));
        stack.Children.Add(row);
        stack.Children.Add(_body);
        Content = stack;
    }

    private async Task CheckAsync(IKutuHost host)
    {
        _body.Children.Clear();
        if (!TryEndpoint(_host.Text, out var name, out var port))
        {
            _body.Children.Add(Chrome.Hint(L.Get("Ssl_Invalid")));
            return;
        }

        _body.Children.Add(Chrome.Loading(L.Get("Common_Working")));
        try
        {
            var report = await Task.Run(() => Inspect(name, port));
            _body.Children.Clear();
            var panel = new StackPanel();
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Protocol"), report.Protocol, () => Copy(host, report.Protocol)));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Subject"), report.Subject, () => Copy(host, report.Subject)));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Issuer"), report.Issuer, () => Copy(host, report.Issuer)));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_San"), report.Sans, () => Copy(host, report.Sans)));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_From"), report.NotBefore, null));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Until"), report.NotAfter, null));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Days"), report.Days, null));
            panel.Children.Add(Chrome.ValueRow(L.Get("Ssl_Chain"), report.Chain, () => Copy(host, report.Chain)));
            _body.Children.Add(Chrome.Section(name + ":" + port, SymbolRegular.LockClosed24, panel));
        }
        catch (Exception ex)
        {
            _body.Children.Clear();
            _body.Children.Add(Chrome.Hint(ex.Message));
        }
    }

    private static void Copy(IKutuHost host, string value)
    {
        host.CopyText(value);
        host.Toast(L.Get("Action_Copied"));
    }

    private static SslReport Inspect(string host, int port)
    {
        using var client = new TcpClient(host, port);
        using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        ssl.AuthenticateAsClient(host);
        var cert = new X509Certificate2(ssl.RemoteCertificate ?? throw new InvalidOperationException("certificate"));
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(cert);
        var names = cert.Extensions.OfType<X509Extension>()
            .Select(item => new AsnEncodedData(item.Oid, item.RawData))
            .Where(item => item.Oid?.Value == "2.5.29.17")
            .Select(item => item.Format(false))
            .FirstOrDefault() ?? "";
        var days = (int)Math.Floor((cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays);
        var chainText = string.Join("  ←  ", chain.ChainElements.Cast<X509ChainElement>().Select(item => item.Certificate.Subject));
        return new SslReport(
            CertificateText.Protocol(ssl.SslProtocol.ToString()),
            cert.Subject,
            cert.Issuer,
            names.Replace("\r", " ").Replace("\n", " "),
            CertificateText.Stamp(cert.NotBefore),
            CertificateText.Stamp(cert.NotAfter),
            days < 0 ? days + " · " + "expired" : days.ToString(),
            chainText);
    }

    private static bool TryEndpoint(string text, out string host, out int port)
    {
        host = "";
        port = 443;
        var value = (text ?? "").Trim();
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            value = value[8..];
        var slash = value.IndexOf('/');
        if (slash >= 0)
            value = value[..slash];
        if (value.Length == 0)
            return false;
        var colon = value.LastIndexOf(':');
        if (colon > 0 && colon < value.Length - 1 && int.TryParse(value[(colon + 1)..], out var parsed) && parsed is > 0 and <= 65535)
        {
            host = value[..colon];
            port = parsed;
        }
        else
        {
            host = value;
        }

        return host.Length > 0 && !host.Contains(' ');
    }

    private sealed record SslReport(string Protocol, string Subject, string Issuer, string Sans, string NotBefore, string NotAfter, string Days, string Chain);
}
