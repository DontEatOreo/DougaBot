using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DougaBot.Modules.DownloadGroup;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed class CompressModel : DownloadModel
{
    [UsedImplicitly]
    [JsonPropertyName("resolution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Resolution { get; set; }

    [UsedImplicitly]
    [Required]
    [JsonPropertyName("crf")]
    public required int Crf { get; init; }
}
