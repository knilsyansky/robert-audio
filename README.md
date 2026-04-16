# YouTube Audio Downloader

Minimal Windows .NET vibecoded application to download YouTube audio, trim by start/end, apply fade in/out, and optionally stabilize volume.

## Requirements
- Windows
- `yt-dlp.exe` in the same folder as the application or on `PATH`
- `ffmpeg.exe` in the same folder as the application or on `PATH`
- .NET 8 runtime installed to run the compiled app

## Build
1. Open Windows Command Prompt or PowerShell in this folder.
2. Run `build.bat` from cmd, or `.
build.ps1` from PowerShell.

If you are in Git Bash, run:

```sh
cmd.exe /c build.bat
```

## Usage
1. Run the built application.
2. Paste the YouTube URL.
3. Set start time and end time in `hh:mm:ss` format.
4. Choose an MP3 output file.
5. Optionally check `Stabilize volume`.
6. Click `Download`.

## Notes
- The app uses `yt-dlp` to download the best audio stream and `ffmpeg` to trim, fade, and convert to MP3.
- If `yt-dlp.exe` or `ffmpeg.exe` cannot be found, place them next to the executable.

## GitHub
1. Create a GitHub repository and push this folder.
2. Add source files, `build.bat`, and `.github/workflows/dotnet.yml`.
3. Use the included GitHub Actions workflow to build on push.

## Installer
A simple Inno Setup script is included as `installer.iss`.
1. Build the app using `build.bat`.
2. Install Inno Setup.
3. Run `iscc installer.iss` to create an installer.
4. The installer will package `YtAudioDownloader.exe`, `yt-dlp.exe`, and `ffmpeg.exe` into an easy Windows setup.

## Release packaging
- Use the `publish` folder as the distributable output.
- Share the complete `publish` folder so users can run `YtAudioDownloader.exe` without installing dependencies.
