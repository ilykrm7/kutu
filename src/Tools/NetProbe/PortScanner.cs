using System.Net.Sockets;
using Kutu.Core.Network;

namespace Kutu.Tools.NetProbe;

public sealed class PortScanResult
{
    public int Port { get; init; }

    public bool Open { get; init; }

    public string Service { get; init; } = "";

    public string Banner { get; init; } = "";
}

public readonly record struct PortScanTick(int Done, int Total, int Open, TimeSpan Eta);

public static class PortScanner
{
    public static async Task RunAsync(
        string host,
        int[] ports,
        int timeoutMs,
        int concurrency,
        Action<PortScanResult> onResult,
        IProgress<PortScanTick>? progress,
        CancellationToken cancellationToken)
    {
        timeoutMs = Math.Max(50, Math.Min(timeoutMs, 10000));
        concurrency = Math.Max(1, Math.Min(concurrency, Math.Max(1, ports.Length)));
        concurrency = Math.Min(concurrency, 1024);
        var done = 0;
        var open = 0;
        var next = 0;
        var started = DateTime.UtcNow;
        var workers = new Task[concurrency];
        for (var worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var index = Interlocked.Increment(ref next) - 1;
                    if (index >= ports.Length)
                        return;

                    var port = ports[index];
                    PortScanResult result;
                    try
                    {
                        result = await ProbeAsync(host, port, timeoutMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        result = new PortScanResult { Port = port };
                    }

                    var count = Interlocked.Increment(ref done);
                    if (result.Open)
                    {
                        Interlocked.Increment(ref open);
                        onResult(result);
                    }
                    if (result.Open || port is 22 or 3389 || count % 48 == 0 || count == ports.Length)
                        Report(progress, count, ports.Length, Volatile.Read(ref open), started);
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // Stopping the scan is a normal outcome.
        }

        Report(progress, Volatile.Read(ref done), ports.Length, Volatile.Read(ref open), started);
    }

    private static void Report(IProgress<PortScanTick>? progress, int done, int total, int open, DateTime started)
    {
        var elapsed = Math.Max(0.001, (DateTime.UtcNow - started).TotalSeconds);
        var eta = done <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(elapsed / done * Math.Max(0, total - done));
        progress?.Report(new PortScanTick(done, total, open, eta));
    }

    private static async Task<PortScanResult> ProbeAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        var banner = await ReadBannerAsync(host, port, timeoutMs, cancellationToken);
        if (banner is null)
            return new PortScanResult { Port = port };

        var service = ServiceProbe.Classify(banner);
        var text = ServiceProbe.Decode(banner);
        if (service is null && string.IsNullOrWhiteSpace(text))
            service = await DetectAsync(host, port, timeoutMs, cancellationToken);

        return new PortScanResult
        {
            Port = port,
            Open = true,
            Service = service ?? "",
            Banner = OneLine(text)
        };
    }

    private static async Task<string?> DetectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        var probeTimeout = Math.Min(timeoutMs, 450);
        var rdp = ProbeAsync(host, port, probeTimeout, ServiceProbe.RdpConnectionRequest, data => ServiceProbe.IsRdpResponse(data), "RDP", cancellationToken);
        var http = ProbeAsync(host, port, probeTimeout, ServiceProbe.HttpProbe, static data => ServiceProbe.ClassifyBanner(ServiceProbe.Decode(data)) == "HTTP", "HTTP", cancellationToken);
        var tls = ProbeAsync(host, port, probeTimeout, ServiceProbe.TlsClientHello, data => ServiceProbe.IsTlsServerHello(data), "HTTPS (TLS)", cancellationToken);
        var results = await Task.WhenAll(rdp, http, tls);
        return results[0] ?? results[1] ?? results[2];
    }

    private static async Task<string?> ProbeAsync(
        string host,
        int port,
        int timeoutMs,
        byte[] request,
        Func<byte[], bool> match,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            if (!await ConnectAsync(client, host, port, timeoutMs, cancellationToken))
                return null;
            var stream = client.GetStream();
            await stream.WriteAsync(request, 0, request.Length, cancellationToken);
            var response = await ReadAsync(stream, 128, timeoutMs, cancellationToken);
            return match(response) ? label : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadBannerAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        if (!await ConnectAsync(client, host, port, timeoutMs, cancellationToken))
            return null;
        try
        {
            return await ReadAsync(client.GetStream(), 256, Math.Min(timeoutMs, 280), cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private static async Task<bool> ConnectAsync(TcpClient client, string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        var connect = client.ConnectAsync(host, port);
        var finished = await Task.WhenAny(connect, Task.Delay(timeoutMs, cancellationToken));
        if (finished != connect || cancellationToken.IsCancellationRequested)
        {
            client.Close();
            _ = connect.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            return false;
        }

        await connect;
        return client.Connected;
    }

    private static async Task<byte[]> ReadAsync(NetworkStream stream, int max, int timeoutMs, CancellationToken cancellationToken)
    {
        var buffer = new byte[max];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        try
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token);
            if (read <= 0)
                return [];
            if (read == buffer.Length)
                return buffer;
            var slice = new byte[read];
            Buffer.BlockCopy(buffer, 0, slice, 0, read);
            return slice;
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    private static string OneLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 180 ? flat : flat[..180];
    }
}
