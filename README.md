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

- DougaBot currently only processes the first video or the selected video in a playlist. It **WILL NOT** download, compress, speed up, or trim the entire playlist.
- If you set the environment variable `IOS_COMPATIBILITY` to true, DougaBot will automatically convert WebM videos to MP4 in the background to ensure compatibility with iOS devices.
- To register global commands, ensure that the environment variable `REGISTER_GLOBAL_COMMANDS` is set to true. For first-time bot users, you should set it to true and then set it to false after the commands are registered.
- Each video is downloaded to the system's default temporary directory.

## Running the bot
- Install [FFmpeg](https://ffmpeg.org/) and [YT-DLP](https://github.com/yt-dlp/yt-dlp)
- Set environment variable ``DOUGA_TOKEN`` to your bot token
- **(OPTIONAL)** Set environment variable ``IOS_COMPATIBILITY`` to true, to automatically convert webm to `.MP4` videos for iOS users in the background.
- Download .NET 7.0 SDK from [here](https://dotnet.microsoft.com/download/dotnet/7.0)
- Run ``dotnet run`` in the project directory
- And you are done!

That's it! If you encounter any issues, please feel free to open an issue on [Issues Page](https://github.com/DontEatOreo/DougaBot/issues). 