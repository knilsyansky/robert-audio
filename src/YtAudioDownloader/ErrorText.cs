namespace YtAudioDownloader;

internal static class ErrorText
{
    // The short message for the error dialog, plus the technical details behind "Copy details".
    public static (string Message, string Details) Describe(Exception error, UiText text) => error switch
    {
        YtDlpException { Failure: YtDlpFailure.Blocked } e => (text.ErrorBlocked, e.Details),
        YtDlpException { Failure: YtDlpFailure.Unavailable } e => (text.ErrorUnavailable, e.Details),
        YtDlpException { Failure: YtDlpFailure.InvalidUrl } e => (text.ErrorInvalidUrl, e.Details),
        YtDlpException { Failure: YtDlpFailure.Network } e => (text.ErrorNetwork, e.Details),
        YtDlpException e => (text.ErrorUnknown, e.Details),
        ToolMissingException e => (string.Format(text.ErrorToolMissing, e.ToolName), e.Message),
        ClipOutOfRangeException e => (string.Format(text.ErrorOutOfRange, TimeInput.FormatDisplay(e.VideoDuration)), e.Message),
        FfmpegException e => (text.ErrorConvert, e.Details),
        _ => (text.ErrorUnknown, error.ToString()),
    };
}
