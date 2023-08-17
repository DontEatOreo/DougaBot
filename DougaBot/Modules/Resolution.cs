using Discord.Interactions;
using JetBrains.Annotations;

namespace DougaBot.Modules;

public enum Resolution
{
    [UsedImplicitly]
    [ChoiceDisplay("144p")]
    P144,

    [UsedImplicitly]
    [ChoiceDisplay("240p")]
    P240,

    [UsedImplicitly]
    [ChoiceDisplay("360p")]
    P360,

    [UsedImplicitly]
    [ChoiceDisplay("480p")]
    P480,

    [UsedImplicitly]
    [ChoiceDisplay("720p")]
    P720,

    [UsedImplicitly]
    [ChoiceDisplay("No Change")]
    None
}