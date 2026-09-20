@echo off
rem Builds publish\YtAudioDownloader.zip. Needs the .NET 10 SDK. Arguments are passed to build.ps1 (e.g. -Version 1.0.0).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
