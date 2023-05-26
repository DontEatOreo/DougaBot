using Serilog;

namespace DougaBot.Services.Video;

public class CatBoxHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    private const string UploadApiLink = "https://litterbox.catbox.moe/resources/internals/api.php";

    public CatBoxHttpClient(HttpClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string> UploadFile(string path)
    {
        await using var stream = File.OpenRead(path);
        using MultipartFormDataContent uploadRequest = new()
        {
            { new StringContent("fileupload"), "reqtype" },
            { new StringContent("24h"), "time" },
            { new StreamContent(stream), "fileToUpload", path }
        };
        using var uploadFilePost = await _client.PostAsync(UploadApiLink, uploadRequest);
        if (!uploadFilePost.IsSuccessStatusCode)
        {
            _logger.Error("Couldn't upload file to catbox");
        }
        var link = await uploadFilePost.Content.ReadAsStringAsync();
        return link;
    }
}