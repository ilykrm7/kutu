using System.Net.Http.Headers;
using System.Text.Json;

namespace Kutu.Tools.Upload;

public static class UploadClient
{
    public static async Task<string> UploadAsync(
        string endpoint,
        string apiKey,
        byte[] data,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("missing-config");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(body.Length > 300 ? body[..300] : body);

        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            return url.GetString() ?? "";

        throw new InvalidOperationException(body.Length > 300 ? body[..300] : body);
    }
}
