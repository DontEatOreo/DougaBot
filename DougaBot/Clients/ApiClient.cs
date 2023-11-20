using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DougaBot.Clients;

public sealed class ApiClient
{
    private readonly HttpClient _client;

    private static readonly Uri UploadApiLink = new("https://litterbox.catbox.moe/resources/internals/api.php");

    private const string ExpiryTime = "24h";

    public ApiClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ApiResult> UploadAsync(MemoryStream fileStream)
    {
        MultipartFormDataContent mpContent = new()
        {
            { new StringContent("fileupload"), "reqtype" },
            { new StringContent(ExpiryTime), "time" },
            { new StreamContent(new MemoryStream(fileStream.ToArray())), "fileToUpload", "file.mp4" }
        };
        HttpRequestMessage request = new(HttpMethod.Post, UploadApiLink) { Content = mpContent };

        var response = await _client.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return new ApiResult
            {
                Uri = new Uri(await response.Content.ReadAsStringAsync()),
                Expiry = ExpiryTime,
                ErrorMessage = null,
                Headers = response.Content.Headers
            };
        if (response.StatusCode is HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout)
            throw new HttpRequestException("The request timed out.");

        var error = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(error)!;
        return new ApiResult { ErrorMessage = errorResponse.Message };
    }

    public async Task<ApiResult> DownloadAsync(Uri endpointUrl, string content)
    {
        StringContent stringContent = new(content)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
        };
        HttpRequestMessage request = new(HttpMethod.Get, endpointUrl) { Content = stringContent };
        request.Headers.Add("User-Agent", "DougaBot");

        // Max Discord Time out is 15 minutes and we give ourselves 1 minute of buffer for uploading
        _client.Timeout = TimeSpan.FromMinutes(14);

        var response = await _client.SendAsync(request);
        if (response.IsSuccessStatusCode is false)
        {
            var error = await response.Content.ReadAsStringAsync();
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(error)!;
            return new ApiResult { ErrorMessage = errorResponse.Message };
        }

        await using var responseContent = await response.Content.ReadAsStreamAsync();

        MemoryStream stream = new();
        await responseContent.CopyToAsync(stream);

        if (stream.Length is 0)
            return new ApiResult { ErrorMessage = "Response content is null." };

        return new ApiResult
        {
            ResponseFile = stream,
            Headers = response.Content.Headers
        };
    }

    public sealed record ErrorResponse([property: Required][property: JsonPropertyName("error")] string Message);
}
