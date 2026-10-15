using System.Text;

namespace Kutu.Core.Network;

/// <summary>Pure classifiers for lightweight service detection. No sockets.</summary>
public static class ServiceProbe
{
    /// <summary>TPKT + X.224 Connection Request + RDP Negotiation Request (PROTOCOL_SSL | PROTOCOL_HYBRID).</summary>
    public static readonly byte[] RdpConnectionRequest =
    [
        0x03, 0x00, 0x00, 0x13,
        0x0e, 0xe0, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x08, 0x00, 0x03, 0x00, 0x00, 0x00
    ];

    public static readonly byte[] HttpProbe = Encoding.ASCII.GetBytes("GET / HTTP/1.0\r\nHost: localhost\r\n\r\n");

    public static byte[] TlsClientHello { get; } = BuildClientHello();

    public static bool IsRdpResponse(byte[]? data, int length = -1)
    {
        var count = length < 0 ? data?.Length ?? 0 : Math.Min(length, data?.Length ?? 0);
        if (data is null || count < 11)
            return false;
        if (data[0] != 0x03 || data[1] != 0x00)
            return false;
        // X.224 Connection Confirm sits immediately after the TPKT header and length indicator.
        return data[5] == 0xD0;
    }

    public static bool IsTlsServerHello(byte[]? data, int length = -1)
    {
        var count = length < 0 ? data?.Length ?? 0 : Math.Min(length, data?.Length ?? 0);
        if (data is null || count < 3 || data[1] != 0x03)
            return false;
        return data[0] is 0x14 or 0x15 or 0x16;
    }

    public static string? ClassifyBanner(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var line = text.Trim();
        if (line.StartsWith("SSH-", StringComparison.OrdinalIgnoreCase))
            return "SSH";
        if (line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            return "HTTP";
        if (line.StartsWith("RFB ", StringComparison.Ordinal))
            return "VNC";
        if (line.StartsWith("+OK", StringComparison.Ordinal))
            return "POP3";
        if (line.StartsWith("* OK", StringComparison.OrdinalIgnoreCase))
            return "IMAP";
        if (line.IndexOf("ESMTP", StringComparison.OrdinalIgnoreCase) >= 0
            || (line.StartsWith("220", StringComparison.Ordinal) && line.IndexOf("SMTP", StringComparison.OrdinalIgnoreCase) >= 0))
            return "SMTP";
        if (line.IndexOf("FTP", StringComparison.OrdinalIgnoreCase) >= 0)
            return "FTP";
        if (line.StartsWith("MYSQL", StringComparison.OrdinalIgnoreCase) || line.IndexOf("mysql_native_password", StringComparison.OrdinalIgnoreCase) >= 0)
            return "MySQL";
        if (line.StartsWith("+PONG", StringComparison.OrdinalIgnoreCase) || line.StartsWith("-ERR", StringComparison.Ordinal) || line.StartsWith("REDIS", StringComparison.OrdinalIgnoreCase))
            return "Redis";
        return null;
    }

    public static string? Classify(byte[]? data, int length = -1)
    {
        if (IsTlsServerHello(data, length))
            return "HTTPS (TLS)";
        if (IsRdpResponse(data, length))
            return "RDP";
        return ClassifyBanner(Decode(data, length));
    }

    public static string Decode(byte[]? data, int length = -1)
    {
        var count = length < 0 ? data?.Length ?? 0 : Math.Min(length, data?.Length ?? 0);
        if (data is null || count == 0)
            return "";

        var builder = new StringBuilder(count);
        for (var i = 0; i < count; i++)
        {
            var value = data[i];
            if (value is 0)
                break;
            if (value is 9 or 10 or 13 || value >= 32)
                builder.Append((char)value);
        }

        return builder.ToString().Trim();
    }

    private static byte[] BuildClientHello()
    {
        // client_version + random + empty session + one cipher + null compression
        var body = new byte[2 + 32 + 1 + 4 + 2];
        body[0] = 0x03;
        body[1] = 0x03;
        for (var i = 0; i < 32; i++)
            body[2 + i] = (byte)i;
        var index = 34;
        body[index++] = 0x00;
        body[index++] = 0x00;
        body[index++] = 0x02;
        body[index++] = 0x00;
        body[index++] = 0x2f;
        body[index++] = 0x01;
        body[index] = 0x00;

        var handshake = new byte[4 + body.Length];
        handshake[0] = 0x01;
        handshake[1] = 0x00;
        handshake[2] = (byte)((body.Length >> 8) & 0xff);
        handshake[3] = (byte)(body.Length & 0xff);
        Buffer.BlockCopy(body, 0, handshake, 4, body.Length);

        var record = new byte[5 + handshake.Length];
        record[0] = 0x16;
        record[1] = 0x03;
        record[2] = 0x01;
        record[3] = (byte)((handshake.Length >> 8) & 0xff);
        record[4] = (byte)(handshake.Length & 0xff);
        Buffer.BlockCopy(handshake, 0, record, 5, handshake.Length);
        return record;
    }
}
