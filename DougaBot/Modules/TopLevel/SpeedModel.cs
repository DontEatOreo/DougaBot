using System.Text.Json.Serialization;
using DougaBot.Modules.DownloadGroup;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed class SpeedModel : DownloadModel
{
    [UsedImplicitly]
    [JsonPropertyName("speed")]
    public required double Speed { get; init; }
}