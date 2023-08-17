## Remote Processing Branch

This branch of DougaBot sends videos and audios to [DougaAPI](https://github.com/DontEatOreo/DougaAPI) to be processed.

To switch to the [local-processing](https://github.com/DontEatOreo/DougaBot/tree/local-processing) branch, run 

```bash
git checkout local-processing
```
in your local repository. Once on the new branch, follow the same instructions in the original README to set up and run the bot.

## AppSettings.json
`appsettings.json` is a configuration file used by DougaBot to store settings. To configure settings for DougaBot, you will need to edit the `appsettings.json` file. The file is structured as a JSON object with various settings.

Here is an example of what the `appsettings.json` file might look like:

```json
{
  "DougaSettings": {
    "DougaApiLink": [ "https://localhost:5001/", "https://dougabot-site.com/" ],
    "AutoConvertWebm": true,
    "RegisterGlobalCommands": true,
    "Crf": 30
  }
}
```

* **DougaApiLink**: The URL of the DougaAPI server that DougaBot should send videos and audios to for processing.
* **AutoConvertWebm**: A boolean value indicating whether or not DougaBot should automatically convert WebM videos to MP4 for iOS compatibility.
* **RegisterGlobalCommands**: A boolean value indicating whether or not DougaBot should register global commands.
* **Crf**: The Constant Rate Factor (CRF) used by FFmpeg to compress videos. A higher value results in lower quality but smaller file sizes.

# DougaBot

DougaBot is a [Discord](https://discord.com/) bot written in C# using the [Discord.Net](https://discordnet.dev/). It utilizes [DougaAPI](https://github.com/DontEatOreo/DougaAPI) to download, compress, speed up, and trim videos and audios.

## Features

- Video downloading
- Video compression
- Video to Audio Conversion
- Speed control for Videos and Audios
- Video and Audio trimming
- Automatic conversion of WebM videos to MP4 for iOS compatibility

## Slash Commands

- `/download video`: Download a video
- `/compress video`: Compress a video
- `/speed`: Adjust the playback speed of a video
- `/trim`: Trim a video
- `/toaudio`: Convert a video to audio

If a file is above the guild file size limit, DougaBot will automatically upload the file to a file hosting service and send a link to the file instead.

## Important

## The bot token is stored in environment variable called ``DOUGA_TOKEN``

## Notes

- DougaBot currently only processes the first video or the selected video in a playlist. It **WILL NOT** download, compress, speed up, or trim the entire playlist.
- DougaBot will store logs at `DougaBot/logs`. It rolls everyday and the retention period is 7 files.

## Running the bot
- Set environment variable `DOUGA_TOKEN` to your bot token
- Download .NET 7.0 SDK from [here](https://dotnet.microsoft.com/download/dotnet/7.0)
- Run ``dotnet run`` in the project directory
- And you are done!

That's it! If you encounter any issues, please feel free to open an issue on [Issues Page](https://github.com/DontEatOreo/DougaBot/issues).
