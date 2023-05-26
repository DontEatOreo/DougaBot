using System.Reflection;
using Discord;
using Serilog;

namespace DougaBot.Services.Video;

public class CatBoxClient
{
    private readonly HttpClient _client;
    private readonly Globals _globals;

    private const string UploadApiLink = "https://litterbox.catbox.moe/resources/internals/api.php";

    public CatBoxClient(HttpClient client, Globals globals)
    {
        _client = client;
        _globals = globals;
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
            var source = MethodBase.GetCurrentMethod()!.Name;
            const string message = "Couldn't upload file to catbox";
            LogMessage logMessage = new(LogSeverity.Error, source, message);
            await _globals.LogAsync(logMessage);
        }
        var link = await uploadFilePost.Content.ReadAsStringAsync();
        return link;
    }
}