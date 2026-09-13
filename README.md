# YouTube Audio Downloader

A small Windows app: paste a YouTube link, choose start and end, get an MP3 of that part
(with a short fade in/out and even volume). Made for one friend; works out of the box on Windows 10/11.

## For users

Download [`YtAudioDownloader.zip`](https://github.com/knilsyansky/robert-audio/releases/latest/download/YtAudioDownloader.zip)
from the latest release, unpack it, run `YtAudioDownloader.exe`. On first start Windows SmartScreen may say
"Windows protected your PC": click **More info → Run anyway**. See the release notes for a short checklist.

- Times: `95`, `1:35`, `0:01:35`, `1:35.5` all work. A link with `&t=95s` (YouTube "Share → Start at") fills Start in.
- Files go to `Music\YtAudioDownloader` as `<video title> (1m35s-2m05s).mp3`; existing files are never overwritten.
- The app keeps its own copy of yt-dlp in `%LOCALAPPDATA%\YtAudioDownloader` and updates it (nightly channel) on every start.
  If YouTube blocks a download, it updates and retries once. Logs: `%LOCALAPPDATA%\YtAudioDownloader\logs`.

## For developers

Needs the .NET 10 SDK.

```powershell
dotnet test YtAudioDownloader.sln                                   # unit tests
.\scripts\fetch-tools.ps1 -Destination .tools-cache\dev-tools       # once, so F5/dotnet run finds yt-dlp, deno, ffmpeg
dotnet run --project src\YtAudioDownloader                          # run the app
.\build.ps1 -Version 1.2.3                                          # publish\YtAudioDownloader.zip (or build.bat)
```

Headless smoke test of a build: `YtAudioDownloader.exe --clip <url> <start> <end> <output folder>` prints `OK <file>` or `ERROR ...`.
Environment overrides: `YTAUDIO_DATA_DIR` (data folder), `YTAUDIO_LANG=ru|en` (UI language).

Layout: `src/YtAudioDownloader` (app), `tests/YtAudioDownloader.Tests` (xUnit), `scripts/` (tool download, screenshots).

## Releasing

Push a tag: `git tag v1.0.0 && git push origin v1.0.0`. GitHub Actions runs `build.ps1`, then publishes a release with
`YtAudioDownloader.zip` (app + yt-dlp nightly + deno + ffmpeg) and `docs/release-notes.md` as the description.
