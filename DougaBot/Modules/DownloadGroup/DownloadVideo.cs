using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
	/// <summary>
	/// Download Video
	/// </summary>
	[SlashCommand("video", "Download Video")]
	public async Task SlashDownloadVideoCommand(string url)
		=> await DeferAsync(options: Options)
			.ContinueWith(async _ => await DownloadVideo(url));

	[MessageCommand("Download Video")]
	public async Task VideoMessageCommand(IMessage message)
	{
		await DeferAsync(options: Options);

		if (message.Attachments.Any())
		{
			foreach (var attachment in message.Attachments)
			{
				if(!attachment.ContentType.StartsWith("video"))
				{
					await FollowupAsync("Invalid file type", options: Options);
					return;
				}

				await DownloadVideo(attachment.Url);
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

			await DownloadVideo(extractUrl);
		}
	}

	private async Task DownloadVideo(string url)
	{
		var runResult = await RunDownload(url,
			TimeSpan.FromHours(2),
			"Video needs to be shorter than 2 hours",
			"Could not download video",
			"Couldn't download video",
			new OptionSet
			{
				FormatSort = FormatSort,
				NoPlaylist = true,
				RemuxVideo = RemuxVideo
			}, Context.Interaction);

		if (runResult is null)
		{
			RateLimitAttribute.ClearRateLimit(Context.User.Id);
			return;
		}

		var videoPath = Path.Combine(DownloadFolder, $"{runResult.ID}.mp4");
		var videoSize = new FileInfo(videoPath).Length / 1048576f;

		if (videoSize > 1024)
		{
			File.Delete(videoPath);
			await FollowupAsync("Video is too big.\nThe video needs to be smaller than 1GB", ephemeral: true, options: Options);
			return;
		}

		await UploadFile(videoSize, videoPath, Context);
	}
}