[Setup]
AppName=YouTube Audio Downloader
AppVersion=1.0
DefaultDirName={pf}\YtAudioDownloader
DefaultGroupName=YouTube Audio Downloader
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=YtAudioDownloaderInstaller

[Files]
Source: "publish\\YtAudioDownloader.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\\yt-dlp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\\YouTube Audio Downloader"; Filename: "{app}\\YtAudioDownloader.exe"

[Run]
Filename: "{app}\\YtAudioDownloader.exe"; Description: "Launch YouTube Audio Downloader"; Flags: nowait postinstall skipifsilent
