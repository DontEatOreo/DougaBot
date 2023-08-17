using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DougaBot.Modules.DownloadGroup;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public class TrimModel : DownloadModel
{
    [UsedImplicitly]
    [Required]
    [JsonPropertyName("start")]
    public required float Start { get; init; }

    [UsedImplicitly]
    [Required]
    [JsonPropertyName("end")]
    public required float End { get; init; }
}