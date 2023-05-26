## Local Processing Branch

This branch of DougaBot downloads and compresses videos and audio locally using FFmpeg and YT-DLP. However, it's recommended that users switch to the [remote-processing](https://github.com/DontEatOreo/DougaBot/tree/remote-processing) branch instead. This alternative branch offloads video and audio processing to a remote server.

To switch to the [remote-processing](https://github.com/DontEatOreo/DougaBot/tree/remote-processing) branch, run 
```bash 
git checkout remote-processing
```
in your local repository. Once on the new branch, follow the same instructions in the original README to set up and run the bot.

# DougaBot

DougaBot is a [Discord](https://discord.com/) bot written in C# using the [Discord.Net](https://discordnet.dev/). It utilizes FFmpeg to compress videos and audio, as well as YT-DLP to download videos from any supported website.

## Features

- Video downloading
- Audio downloading
- Video compression
- Audio compression
- Speed control for videos
- Video trimming
- Automatic conversion of WebM videos to MP4 for iOS compatibility

## Slash Commands

- `/download video`: Download a video
- `/download audio`: Download an audio file
- `/compress video`: Compress a video
- `/compress audio`: Compress an audio file
- `/speed`: Adjust the playback speed of a video
- `/trim`: Trim a video

## Important
### The bot token is stored in environment variable called ``DOUGA_TOKEN``

## Notes

- By default DougaBot will assume `ffmpeg` and `yt-dlp` are in the path. If they are not, you can set the environment variables `FFMPEG_PATH` and `YTDLP_PATH` to the path of the executables.
- DougaBot **only** processes the first video or the selected video in a playlist. It **WILL NOT** download, compress, speed up, or trim the entire playlist.
- Each video is downloaded to the system's default temporary directory.

## AppSettings Configuration

The `appsettings.json` file contains configuration settings for the application. Here's an overview of the available properties and their purpose:

- `ffmpeg_path`: Specifies the path to the FFmpeg executable. It is used for video processing tasks.
- `yt_dlp_path`: Specifies the path to the yt-dlp executable. It is used for downloading videos.
- `ios_compatability`: Indicates whether iOS compatibility mode is enabled. When enabled, the output videos are optimized for iOS devices (This means videos are playable on iOS and have audio on Discord).
- `register_global_commands`: Specifies whether global commands should be registered. Enabling this allows the application to respond to system-wide commands.
- `crf`: Controls the Constant Rate Factor (CRF) value for video compression. Higher values result in lower video quality and smaller file sizes. (A sweet spot is between 26-32)

To configure the application, modify the corresponding values in the `appsettings.json` file. Here are some example values:

```json
{
  "ffmpeg_path": "ffmpeg",
  "yt_dlp_path": "yt-dlp",
  "ios_compatability" : true,
  "register_global_commands": true,
  "crf": 29
}
```

Feel free to adjust these settings based on your requirements and environment.

## Running the bot
- Install [FFmpeg](https://ffmpeg.org/) and [YT-DLP](https://github.com/yt-dlp/yt-dlp)
- Set environment variable ``DOUGA_TOKEN`` to your bot token
- **(OPTIONAL)** Set environment variable ``IOS_COMPATIBILITY`` to true, to automatically convert webm to `.MP4` videos for iOS users in the background.
- Download .NET 7.0 SDK from [here](https://dotnet.microsoft.com/download/dotnet/7.0)
- Run ``dotnet run`` in the project directory
- And you are done!

That's it! If you encounter any issues, please feel free to open an issue on [Issues Page](https://github.com/DontEatOreo/DougaBot/issues). 