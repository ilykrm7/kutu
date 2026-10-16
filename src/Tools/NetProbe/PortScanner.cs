using System.Net;
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
        timeoutMs = PortScanBudget.Timeout(timeoutMs);
        concurrency = PortScanBudget.Concurrency(concurrency);
        var started = DateTime.UtcNow;
        if (ports.Length == 0)
        {
            Report(progress, 0, 0, 0, started);
            return;
        }

        var address = await ResolveAsync(host, cancellationToken);
        if (address is null || cancellationToken.IsCancellationRequested)
        {
            Report(progress, ports.Length, ports.Length, 0, started);
            return;
        }

        var done = 0;
        var open = 0;
        var seen = new HashSet<int>();
        var gate = new SemaphoreSlim(concurrency);
        var tasks = new List<Task>(ports.Length);
        foreach (var port in ports)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var captured = port;
            tasks.Add(Task.Run(() =>
            {
                var opened = false;
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var result = Probe(address, captured, timeoutMs);
                    if (!result.Open)
                        return;

                    lock (seen)
                    {
                        if (!seen.Add(result.Port))
                            return;
                        Interlocked.Increment(ref open);
                        opened = true;
                    }

                    onResult(result);
                }
                catch (OperationCanceledException)
                {
                    // The scan is stopping.
                }
                catch
                {
                    // A failed probe is a closed port.
                }
                finally
                {
                    var count = Interlocked.Increment(ref done);
                    if (opened || count == ports.Length || count % 64 == 0)
                        Report(progress, count, ports.Length, Volatile.Read(ref open), started);
                    gate.Release();
                }
            }));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Stopping the scan is a normal outcome.
        }

        var finished = cancellationToken.IsCancellationRequested ? Volatile.Read(ref done) : ports.Length;
        Report(progress, finished, ports.Length, Volatile.Read(ref open), started);
    }

    private static async Task<IPAddress?> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
            return literal;

        try
        {
            var lookup = Dns.GetHostAddressesAsync(host);
            var finished = await Task.WhenAny(lookup, Task.Delay(3000, cancellationToken));
            if (finished != lookup)
                return null;
            var answers = await lookup;
            return answers.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork)
                ?? answers.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static PortScanResult Probe(IPAddress address, int port, int timeoutMs)
    {
        var answer = Connect(address, port, timeoutMs, out var socket);
        if (PortScanBudget.ShouldRetry(answer, 0))
        {
            socket?.Dispose();
            answer = Connect(address, port, PortScanBudget.RetryTimeout(timeoutMs), out socket);
        }

        if (answer != PortAnswer.Open || socket is null)
        {
            socket?.Dispose();
            return new PortScanResult { Port = port };
        }

        try
        {
            var banner = Read(socket, Math.Min(timeoutMs, 350), 256);
            var service = ServiceProbe.Classify(banner);
            byte[]? rdp = null;
            byte[]? ssh = null;
            byte[]? tls = null;
            if (service is null)
            {
                if (banner.Length == 0)
                {
                    socket.SendTimeout = timeoutMs;
                    try
                    {
                        socket.Send(ServiceProbe.RdpConnectionRequest);
                        rdp = Read(socket, Math.Min(timeoutMs, 500), 128);
                    }
                    catch
                    {
                        rdp = null;
                    }
                }
                else
                {
                    rdp = Exchange(address, port, timeoutMs, ServiceProbe.RdpConnectionRequest);
                }

                if (!ServiceProbe.IsRdpResponse(rdp))
                {
                    ssh = Exchange(address, port, timeoutMs, ServiceProbe.SshClientBanner);
                    if (ServiceProbe.Classify(ssh) is null)
                        tls = Exchange(address, port, timeoutMs, ServiceProbe.TlsClientHello);
                }
            }

            service = ServiceProbe.Identify(banner, rdp, ssh, tls);
            return new PortScanResult
            {
                Port = port,
                Open = true,
                Service = service ?? "",
                Banner = OneLine(ServiceProbe.Decode(banner))
            };
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static PortAnswer Connect(IPAddress address, int port, int timeoutMs, out Socket? socket)
    {
        socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var result = socket.BeginConnect(new IPEndPoint(address, port), null, null);
            if (!result.AsyncWaitHandle.WaitOne(Math.Max(1, timeoutMs)))
            {
                Close(socket);
                try
                {
                    socket.EndConnect(result);
                }
                catch
                {
                    // The timed-out connect is aborted.
                }

                socket = null;
                return PortAnswer.Unanswered;
            }

            socket.EndConnect(result);
            if (!socket.Connected)
            {
                Close(socket);
                socket = null;
                return PortAnswer.Refused;
            }

            return PortAnswer.Open;
        }
        catch (SocketException)
        {
            Close(socket);
            socket = null;
            return PortAnswer.Refused;
        }
        catch
        {
            Close(socket);
            socket = null;
            return PortAnswer.Unanswered;
        }
    }

    private static byte[] Exchange(IPAddress address, int port, int timeoutMs, byte[] request)
    {
        var answer = Connect(address, port, timeoutMs, out var socket);
        if (answer != PortAnswer.Open || socket is null)
        {
            socket?.Dispose();
            return [];
        }

        try
        {
            socket.SendTimeout = timeoutMs;
            socket.Send(request);
            return Read(socket, Math.Min(timeoutMs, 500), 128);
        }
        catch
        {
            return [];
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static byte[] Read(Socket socket, int timeoutMs, int max)
    {
        try
        {
            if (!socket.Poll(Math.Max(1, timeoutMs) * 1000, SelectMode.SelectRead))
                return [];
            if (socket.Available <= 0)
                return [];
            var buffer = new byte[Math.Min(max, socket.Available)];
            var read = socket.Receive(buffer);
            if (read <= 0)
                return [];
            if (read == buffer.Length)
                return buffer;
            var slice = new byte[read];
            Buffer.BlockCopy(buffer, 0, slice, 0, read);
            return slice;
        }
        catch
        {
            return [];
        }
    }

    private static void Close(Socket? socket)
    {
        try
        {
            socket?.Close();
        }
        catch
        {
            // The socket is already gone.
        }
    }

    private static void Report(IProgress<PortScanTick>? progress, int done, int total, int open, DateTime started)
    {
        var elapsed = Math.Max(0.001, (DateTime.UtcNow - started).TotalSeconds);
        var eta = done <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(elapsed / done * Math.Max(0, total - done));
        progress?.Report(new PortScanTick(done, total, open, eta));
    }

    private static string OneLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 180 ? flat : flat[..180];
    }
}
