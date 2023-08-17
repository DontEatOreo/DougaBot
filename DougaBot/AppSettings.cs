using System.ComponentModel.DataAnnotations;

namespace DougaBot;

public sealed class AppSettings
{
    [Required]
    public required Uri[] DougaApiLink { get; init; }

    [Required]
    public required bool RegisterGlobalCommands { get; init; }

    [Required]
    public required bool AutoConvertWebM { get; init; }

    [Required]
    public required int Crf { get; init; }
}
