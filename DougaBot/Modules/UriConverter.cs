using Discord;
using Discord.Interactions;

namespace DougaBot.Modules;

public sealed class UriConverter<T> : TypeConverter<T> where T : Uri
{
    private const string InvalidUri = "Value was not a valid uri.";

    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        var value = option.Value;
        var isNotUri = value is not string || !Uri.TryCreate(value.ToString(), UriKind.RelativeOrAbsolute, out _);
        if (isNotUri)
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ParseFailed, InvalidUri));
        var taskSuccess = TypeConverterResult.FromSuccess(new Uri(value.ToString() ?? string.Empty));
        var taskError = TypeConverterResult.FromError(InteractionCommandError.ParseFailed, InvalidUri);
        return Task.FromResult(value is string ? taskSuccess : taskError);
    }
}