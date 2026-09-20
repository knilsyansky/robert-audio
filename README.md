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

### If Windows blocks it (for whoever sets this up)

On a clean Windows 11 install, Smart App Control can block an unsigned app outright, with no "Run anyway" button (SmartScreen's
"More info → Run anyway" only appears when Smart App Control is off). The options are: turn Smart App Control off in
Windows Security → App & browser control (it's a security feature, and on some Windows versions it can't be turned back on
without resetting Windows), or sign the app. Separately, some antivirus products flag `yt-dlp.exe`; if that happens, restore the
quarantined file and add the app's folder to the antivirus exclusions.

## For developers

Needs the .NET 10 SDK.

```powershell
dotnet build YtAudioDownloader.sln                                  # build
.\scripts\fetch-tools.ps1 -Destination .tools-cache\dev-tools       # once, so F5/dotnet run finds yt-dlp, deno, ffmpeg
dotnet run --project src\YtAudioDownloader                          # run the app
.\build.ps1 -Version 1.2.3                                          # publish\YtAudioDownloader.zip (or build.bat)
```

Headless smoke test of a build (it's a GUI app, so running it plain from `cmd` prints nothing and `cmd` won't wait for it):

```powershell
Start-Process .\YtAudioDownloader.exe -ArgumentList '--clip','<url>','1:00','1:30','C:\clips' -Wait -NoNewWindow -RedirectStandardOutput out.txt
Get-Content out.txt -Encoding UTF8    # "OK <file>" or "ERROR ..."
```

Environment overrides: `YTAUDIO_DATA_DIR` (data folder), `YTAUDIO_LANG=ru|en` (UI language).

Layout: `src/YtAudioDownloader` (app), `scripts/` (tool download, screenshots). The xUnit tests in
`tests/YtAudioDownloader.Tests` are kept out of the repository (see `.gitignore`); where they exist locally,
`dotnet test tests\YtAudioDownloader.Tests\YtAudioDownloader.Tests.csproj` runs them and `build.ps1` picks them up
automatically.

## Releasing

Push a tag: `git tag v1.0.0 && git push origin v1.0.0`. GitHub Actions runs `build.ps1`, then publishes a release with
`YtAudioDownloader.zip` (app + yt-dlp nightly + deno + ffmpeg) and `docs/release-notes.md` as the description.
