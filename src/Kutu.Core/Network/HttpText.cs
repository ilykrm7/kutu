using System.Net.Http;

namespace Kutu.Core.Network;

public static class HttpText
{
    static HttpText() => TlsPolicy.Enable();

    public static async Task<string> GetStringAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}
