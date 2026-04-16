@echo off
REM Build the WinForms app and publish a self-contained Windows EXE.
REM This script will try to refresh yt-dlp.exe if it is missing or broken.

setlocal
if not defined DOTNET_ROOT (set DOTNET_ROOT=%ProgramFiles%\dotnet)
if exist "%DOTNET_ROOT%\dotnet.exe" (
    echo Using dotnet at %DOTNET_ROOT%\dotnet.exe
) else (
    echo ERROR: .NET SDK not found. Install .NET 8 SDK and rerun.
    exit /b 1
)

if not exist yt-dlp.exe (
    echo yt-dlp.exe not found. Downloading latest copy...
    powershell.exe -NoProfile -Command "(New-Object System.Net.WebClient).DownloadFile('https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe','yt-dlp.exe')"
)

yt-dlp.exe --version >nul 2>&1
if errorlevel 1 (
    echo yt-dlp.exe appears corrupted or broken. Re-downloading...
    del /f /q yt-dlp.exe >nul 2>&1
    powershell.exe -NoProfile -Command "(New-Object System.Net.WebClient).DownloadFile('https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe','yt-dlp.exe')"
)

yt-dlp.exe --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: yt-dlp.exe is still not working. Delete yt-dlp.exe and try again.
    exit /b 1
)

echo yt-dlp.exe is ready.

tasklist /fi "imagename eq YtAudioDownloader.exe" | findstr /i "YtAudioDownloader.exe" >nul 2>&1
if not errorlevel 1 (
    echo Closing running YtAudioDownloader.exe...
    taskkill /f /im YtAudioDownloader.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

if exist publish (
    echo Removing previous publish folder...
    rmdir /s /q publish
)

"%DOTNET_ROOT%\dotnet.exe" publish YtAudioDownloader.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

copy /Y yt-dlp.exe publish\ >nul 2>&1
copy /Y ffmpeg.exe publish\ >nul 2>&1

echo Build complete. Output folder: %CD%\publish
endlocal

