namespace YtAudioDownloader;

internal sealed class ToolMissingException(string toolName, string expectedPath)
    : Exception($"{toolName} was not found at {expectedPath}")
{
    public string ToolName { get; } = toolName;
}

internal sealed class YtDlpException(YtDlpFailure failure, string details)
    : Exception($"yt-dlp failed ({failure})")
{
    public YtDlpFailure Failure { get; } = failure;
    public string Details { get; } = details;
}

internal sealed class FfmpegException(string details) : Exception("ffmpeg failed")
{
    public string Details { get; } = details;
}

internal sealed class ClipOutOfRangeException(TimeSpan videoDuration)
    : Exception($"The start time is after the end of the video ({videoDuration})")
{
    public TimeSpan VideoDuration { get; } = videoDuration;
}
