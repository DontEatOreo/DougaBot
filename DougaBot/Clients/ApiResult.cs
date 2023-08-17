using System.Net.Http.Headers;

namespace DougaBot.Clients;

public sealed class ApiResult
{
    public Uri? Uri { get; init; }

    public string? Expiry { get; init; }

    public MemoryStream? ResponseFile { get; init; }

    public string? ErrorMessage { get; init; }

    public HttpContentHeaders? Headers { get; init; }
}
