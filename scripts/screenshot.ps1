# Starts the app, captures its window to a PNG and closes it. Used to check the layout in both languages.
# Usage: scripts\screenshot.ps1 -Exe <path to YtAudioDownloader.exe> -Lang ru -Out window-ru.png
param(
    [Parameter(Mandatory)][string]$Exe,
    [ValidateSet('ru', 'en')][string]$Lang = 'en',
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ScreenshotNative
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@

$env:YTAUDIO_LANG = $Lang
$process = Start-Process -FilePath $Exe -PassThru
try {
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 300
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'The window did not appear within 15 seconds.' }
    Start-Sleep -Seconds 2   # let the startup status text settle

    $rect = New-Object ScreenshotNative+RECT
    [ScreenshotNative]::GetWindowRect($process.MainWindowHandle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [ScreenshotNative]::PrintWindow($process.MainWindowHandle, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Saved $Out"
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}
