using System.ComponentModel.DataAnnotations;
using DougaBot.Modules.DownloadGroup;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed class ToAudioModel : DownloadModel
{
    [UsedImplicitly]
    [Required]
    public required string Format { get; init; }
}