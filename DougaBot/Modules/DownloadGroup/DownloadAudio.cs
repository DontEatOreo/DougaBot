using Discord;
using DougaBot.PreConditions;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
	/// <summary>
	/// Download Audio
	/// </summary>
	public async Task SlashCompressAudioCommand(string url)
		=> await DeferAsync(options: Options)
			.ContinueWith(async _ => await DownloadAudio(url));

	public async Task AudioMessageCommand(IMessage message)
	{
		await DeferAsync(options: Options);

		if (message.Attachments.Any())
		{
			foreach (var attachment in message.Attachments)
			{
				if(!attachment.ContentType.StartsWith("audio") ||
				   !attachment.ContentType.StartsWith("video"))
				{
					await FollowupAsync("Invalid file type", options: Options);
					return;
				}

				await DownloadAudio(attachment.Url);
			}
		}
		else
		{
			var extractUrl = await ExtractUrl(message.Content, Context.Interaction);
			if (extractUrl is null)
			{
				await FollowupAsync("No URL found", options: Options);
				return;
			}

			await DownloadAudio(extractUrl);
		}
	}

	private async Task DownloadAudio(string url)
	{
		var runDownload = await RunDownload(url,
			TimeSpan.FromHours(2),
			"Audio is too long.\nThe audio needs to be shorter than 2 hours",
			"Could not fetch data",
			"Could not download audio",
			new OptionSet
			{
				ExtractAudio = true,
				AudioFormat = AudioConversionFormat.M4a,
				NoPlaylist = true,
			}, Context.Interaction);

		if (runDownload is null)
		{
			RateLimitAttribute.ClearRateLimit(Context.User.Id);
			return;
		}

		var audioPath = Path.Combine(DownloadFolder, $"{runDownload.ID}.m4a");
		var audioSize = new FileInfo(audioPath).Length / 1048576f;

		if (audioSize > 1024)
		{
			File.Delete(audioPath);
			await FollowupAsync("Audio is too big.\nThe audio needs to be smaller than 1GB", ephemeral: true, options: Options);
			return;
		}

		await UploadFile(audioSize, audioPath, Context);
	}
}