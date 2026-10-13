using System.IO.Pipes;
using System.Text.Json;

namespace Kutu.App.Services;

public static class SingleInstance
{
    public const string MutexName = @"Local\Kutu.SingleInstance.v1";
    public const string PipeName = "Kutu.SingleInstance.v1";

    public static void Send(string[] args)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(2000);
        using var writer = new StreamWriter(client) { AutoFlush = true };
        writer.Write(JsonSerializer.Serialize(args));
    }

    public static void StartServer(Action<string[]> onMessage, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(cancellationToken);
                    using var reader = new StreamReader(server);
                    var payload = await reader.ReadToEndAsync(cancellationToken);
                    var args = JsonSerializer.Deserialize<string[]>(payload) ?? [];
                    onMessage(args);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(200, cancellationToken);
                }
            }
        }, cancellationToken);
    }
}
