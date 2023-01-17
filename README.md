# DougaBot

DougaBot is a **[Discord](https://discord.com/)** bot written in **C#** using [Discord.Net](https://discordnet.dev/) library.

It utilizes **[FFmpeg](https://ffmpeg.org/)** to compress videos or audios and **[YT-DLP](https://github.com/yt-dlp/yt-dlp)** to download videos from any supported website.

## Features
- Download Videos
- Download Audios
- Compress Videos
- Compress Audios
- Speed up/Slow down Videos
- Trim Videos
- Automatically convert webm to mp4 videos for iOS users in the background

## Slash Commands
``/download video`` download a video

``/download audio`` download an audio

``/compress video`` compress a video

``/compress audio`` compress an audio

``/speed`` change the speed of a video

``/trim`` trim a video

## Notes
- Currently DougaBot will not download, compress, speed or trim playlists. (Instead it will only process the first video or the video that's selected in the playlist)
- You can set environment variable ``USE_HARDWARE_ACCELERATION`` to true, to use hardware acceleration for video compression.
- You can set environment variable ``IOS_COMPATIBILITY`` to true, to automatically convert webm to mp4 videos for iOS users in the background.
- Don't forget to set environment variable ``REGISER_GLOBAL_COMMANDS`` to true, to register global commands. (If you are running the bot for first time, you should set it to true, then set it to false after the commands are registered)
- Every video is downloaded to the default system temp directory.

## NuGet Packages
```
Discord.Net.Core
Discord.Net.Interactions
Discord.Net.WebSocket
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Hosting
Serilog
Serilog.Extensions.Hosting
Serilog.Settings.Configuration
Serilog.Sinks.Console
Xabe.FFmpeg
YoutubeDLSharp
```