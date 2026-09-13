# Downloads the tools the app runs into -Destination: yt-dlp (latest nightly), deno and ffmpeg (pinned) and their license notes.
# Works in Windows PowerShell 5.1 and PowerShell 7. Big pinned downloads are cached in .tools-cache\.
param(
    [Parameter(Mandatory)][string]$Destination,
    [string]$CacheDir = (Join-Path $PSScriptRoot '..\.tools-cache')
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$DenoVersion = 'v2.9.6'
$FfmpegVersion = '8.1'
$YtDlpUrl = 'https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe'
$DenoUrl = "https://github.com/denoland/deno/releases/download/$DenoVersion/deno-x86_64-pc-windows-msvc.zip"
$FfmpegUrl = "https://github.com/GyanD/codexffmpeg/releases/download/$FfmpegVersion/ffmpeg-$FfmpegVersion-essentials_build.zip"

New-Item -ItemType Directory -Force $Destination, $CacheDir | Out-Null
$licenses = Join-Path $Destination 'licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null

function Get-Cached([string]$Url, [string]$FileName) {
    $path = Join-Path $CacheDir $FileName
    if (-not (Test-Path $path)) {
        Write-Host "Downloading $Url"
        Invoke-WebRequest -Uri $Url -OutFile "$path.partial" -UseBasicParsing
        Move-Item "$path.partial" $path -Force
    }
    return $path
}

# yt-dlp: never cached, always the newest nightly (the app keeps its own copy updated afterwards).
Write-Host "Downloading $YtDlpUrl"
Invoke-WebRequest -Uri $YtDlpUrl -OutFile (Join-Path $Destination 'yt-dlp.exe') -UseBasicParsing

$denoZip = Get-Cached $DenoUrl "deno-$DenoVersion.zip"
Expand-Archive $denoZip -DestinationPath $Destination -Force

$ffmpegZip = Get-Cached $FfmpegUrl "ffmpeg-$FfmpegVersion-essentials_build.zip"
$ffmpegDir = Join-Path $CacheDir "ffmpeg-$FfmpegVersion"
if (-not (Test-Path $ffmpegDir)) { Expand-Archive $ffmpegZip -DestinationPath $ffmpegDir -Force }
$ffmpegRoot = Get-ChildItem $ffmpegDir -Directory | Select-Object -First 1
Copy-Item (Join-Path $ffmpegRoot.FullName 'bin\ffmpeg.exe') $Destination -Force
Copy-Item (Join-Path $ffmpegRoot.FullName 'LICENSE') (Join-Path $licenses 'ffmpeg-LICENSE.txt') -Force

@"
Third-party programs in this folder
===================================

yt-dlp (nightly) - The Unlicense (public domain)
  https://github.com/yt-dlp/yt-dlp

Deno $DenoVersion - MIT License
  https://github.com/denoland/deno

FFmpeg $FfmpegVersion essentials build by gyan.dev - GPL v3 (see ffmpeg-LICENSE.txt)
  Build:  https://github.com/GyanD/codexffmpeg/releases/tag/$FfmpegVersion
  Source: https://ffmpeg.org/releases/ffmpeg-$FfmpegVersion.tar.xz
"@ | Set-Content -Path (Join-Path $licenses 'THIRD-PARTY.txt') -Encoding UTF8

foreach ($tool in 'yt-dlp.exe', 'deno.exe', 'ffmpeg.exe') {
    if (-not (Test-Path (Join-Path $Destination $tool))) { throw "$tool is missing after download" }
}
Write-Host "Tools ready in $Destination"
