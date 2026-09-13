# Tests, publishes a self-contained single-file build, adds the tools and zips it:
#   publish\YtAudioDownloader\  and  publish\YtAudioDownloader.zip
param([string]$Version = '0.0.0-dev')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publish = Join-Path $root 'publish'
$appDir = Join-Path $publish 'YtAudioDownloader'
$zip = Join-Path $publish 'YtAudioDownloader.zip'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet test (Join-Path $root 'YtAudioDownloader.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

dotnet publish (Join-Path $root 'src\YtAudioDownloader\YtAudioDownloader.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:Version=$Version -o $appDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

& (Join-Path $root 'scripts\fetch-tools.ps1') -Destination (Join-Path $appDir 'tools')
Copy-Item (Join-Path $root 'docs\user-readme.txt') (Join-Path $appDir 'README.txt')

Compress-Archive -Path $appDir -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Done: $zip"
