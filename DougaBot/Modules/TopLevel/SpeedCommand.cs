using System.Reflection;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using Serilog.Events;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel
{
	private async Task SpeedQueueHandler(string url, string speed)
	{
		var userLock = TopLevel.QueueLocks.GetOrAdd(Context.User.Id, _ => new SemaphoreSlim(1, 1));

		await userLock.WaitAsync();
		try
		{
			// Run the task
			Log.Write(LogEventLevel.Information,
				"[{Source}] {Message}",
				MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
				$"{Context.User.Username}#{Context.User.Discriminator} locked: {url} at {speed} speed");
			await SpeedTask(url, speed);
		}
		catch (Exception e)
		{
			Log.Error(e, "[{Source}] {Message}", MethodBase.GetCurrentMethod()?.DeclaringType?.Name, e.Message);
		}
		finally
		{
			// Release the lock
			Log.Write(LogEventLevel.Information,
				"[{Source}] {Message}",
				MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
				$"{Context.User.Username}#{Context.User.Discriminator} released: {url} at {speed} speed");
			userLock.Release();
		}
	}

	[SlashCommand("speed", "Change the speed of a video/audio")]
	public async Task SpeedCmd(string url,
		[Choice("0.5x", "0.5x"),
		 Choice("1.5x", "1.5x"),
		 Choice("2x", "2x"),
		 Choice("4x", "4x")] string speed)
		=> await DeferAsync(options: Options)
			.ContinueWith(async _ => await SpeedQueueHandler(url, speed));

	private async Task SpeedTask(string url, string speed)
	{
		var runDownload = await RunDownload(url, TimeSpan.FromHours(2),
			"The Video or Audio needs to be shorter than 2 hours",
			"Couldn't fetch video or audio data",
			"There was an error downloading the file\nPlease try again later",
			new OptionSet
			{
				FormatSort = FormatSort,
				NoPlaylist = true,
				RemuxVideo = TopLevel.RemuxVideo
			}, Context.Interaction);
		if (runDownload is null)
		{
			RateLimitAttribute.ClearRateLimit(Context.User.Id);
			return;
		}

		var folderUuid = Guid.NewGuid().ToString()[..4];
		var beforeFile = Path.Combine(DownloadFolder, $"{runDownload.ID}.mp4");
		var afterFile = Path.Combine(DownloadFolder, folderUuid, $"{runDownload.ID}.mp4");

		// get stream info about before file
		var beforeStreamInfo = FFmpeg.GetMediaInfo(beforeFile).Result;
		var videoStreams = beforeStreamInfo.VideoStreams.First();
		var audioStreams = beforeStreamInfo.AudioStreams.First();

		/*
		 * Template:
		 * ffmpeg -i beforeFile -filter_complex "[0:v]setpts=0.5*PTS[v];[0:a]atempo=2.0[a]" -map "[v]" -map "[a]" afterFile
		 *
		 * Higher PTS = slower video
		 * Lower PTS = faster video
		 *
		 * Higher atempo = faster audio
		 * Lower atempo = slower audio
		 *
		 * "atempo" has a limited range between 0.5 to 2.0.
		 * For example if we want to slow down the audio by 4x, we need to use atempo=2.0 twice. (atempo=2.0,atempo=2.0)
		 * "setpts" does not have this limitation.
		 */

		Dictionary<string, (string setpts, string atempo)> speedMap = new()
		{
			{ "0.5x", ("2*PTS", "atempo=0.5") },
			{ "1.5x", ("0.6666666667*PTS", "atempo=1.5") },
			{ "2x", ("0.5*PTS", "atempo=2.0") }
		};

		var (setpts, atempo) = speedMap[speed];

		string ffmpegArgs;

		if (videoStreams is null && audioStreams is null)
		{
			await FollowupAsync("Invalid file", ephemeral: true, options: Options);
			return;
		}

		if (videoStreams is null)
		{
			ffmpegArgs = $"-i {beforeFile} filter:a \"{atempo}\" -vn {afterFile}";
			await FFmpeg.Conversions.New()
				.AddParameter(ffmpegArgs)
				.Start();
		}
		else
		{
			ffmpegArgs =
				$"-i {beforeFile} -c:v libx264 -filter_complex \"[0:v]setpts={setpts}[v];[0:a]{atempo}[a]\" -map \"[v]\" -map \"[a]\" {afterFile}";
			await FFmpeg.Conversions.New()
				.AddParameter(ffmpegArgs)
				.Start();
		}

		var afterFileSize = new FileInfo(afterFile).Length / 1048576f;

		// if a video is longer than 2 hours delete it
		var duration = FFmpeg.GetMediaInfo(afterFile).Result.Duration;
		if (duration > TimeSpan.FromHours(2))
		{
			File.Delete(afterFile);
			await FollowupAsync("The Video or Audio needs to be shorter than 2 hours", options: Options, ephemeral: true);
			return;
		}

		await UploadFile(afterFileSize, afterFile, Context);
	}
}