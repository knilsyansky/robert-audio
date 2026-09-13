using System.Globalization;

namespace YtAudioDownloader;

// Every text the window shows. "required" makes a missing translation a compile error.
internal sealed record UiText
{
    public required string WindowTitle { get; init; }
    public required string UrlLabel { get; init; }
    public required string StartLabel { get; init; }
    public required string EndLabel { get; init; }
    public required string TimeHint { get; init; }
    public required string FadeCheckBox { get; init; }
    public required string NormalizeCheckBox { get; init; }
    public required string OutputFolderLabel { get; init; }
    public required string BrowseButton { get; init; }
    public required string FolderDialogDescription { get; init; }
    public required string DownloadButton { get; init; }
    public required string CancelButton { get; init; }
    public required string OpenFolderButton { get; init; }
    public required string StatusReady { get; init; }
    public required string StatusUpdating { get; init; }
    public required string StatusDownloading { get; init; }
    public required string StatusConverting { get; init; }
    public required string StatusDone { get; init; }
    public required string StatusCancelled { get; init; }
    public required string StatusError { get; init; }
    public required string ToolChecking { get; init; }
    public required string ToolOk { get; init; }
    public required string ToolUpdateFailed { get; init; }
    public required string ToolMissing { get; init; }
    public required string ErrorDialogTitle { get; init; }
    public required string CopyDetailsButton { get; init; }
    public required string Copied { get; init; }
    public required string DetailsExpander { get; init; }
    public required string ErrorNoUrl { get; init; }
    public required string ErrorBadStart { get; init; }
    public required string ErrorBadEnd { get; init; }
    public required string ErrorEndBeforeStart { get; init; }
    public required string ErrorBlocked { get; init; }
    public required string ErrorUnavailable { get; init; }
    public required string ErrorInvalidUrl { get; init; }
    public required string ErrorNetwork { get; init; }
    public required string ErrorToolMissing { get; init; }
    public required string ErrorBadFolder { get; init; }
    public required string ErrorOutOfRange { get; init; }
    public required string ErrorConvert { get; init; }
    public required string ErrorUnknown { get; init; }
}

internal static class Strings
{
    public static UiText English { get; } = new()
    {
        WindowTitle = "YouTube Audio Downloader",
        UrlLabel = "YouTube link",
        StartLabel = "Start",
        EndLabel = "End",
        TimeHint = "e.g. 1:35 or 95 (seconds)",
        FadeCheckBox = "Fade in/out, seconds:",
        NormalizeCheckBox = "Even out the volume",
        OutputFolderLabel = "Save to folder",
        BrowseButton = "Browse...",
        FolderDialogDescription = "Where to save the MP3 files",
        DownloadButton = "Download",
        CancelButton = "Cancel",
        OpenFolderButton = "Open folder",
        StatusReady = "Ready.",
        StatusUpdating = "Updating yt-dlp...",
        StatusDownloading = "Downloading audio...",
        StatusConverting = "Cutting and converting...",
        StatusDone = "Saved: {0}",
        StatusCancelled = "Cancelled.",
        StatusError = "Error.",
        ToolChecking = "yt-dlp: checking for updates...",
        ToolOk = "yt-dlp {0}",
        ToolUpdateFailed = "yt-dlp {0} · update check failed",
        ToolMissing = "yt-dlp not found",
        ErrorDialogTitle = "Couldn't save the clip",
        CopyDetailsButton = "Copy details",
        Copied = "Copied",
        DetailsExpander = "Details",
        ErrorNoUrl = "Paste a YouTube link first.",
        ErrorBadStart = "Can't read the start time. Examples: 1:35 or 95.",
        ErrorBadEnd = "Can't read the end time. Examples: 2:05 or 125.",
        ErrorEndBeforeStart = "The end must be later than the start.",
        ErrorBlocked = "YouTube blocked the download. Try again in a few minutes. If it keeps happening, click \"Copy details\" and send the text to whoever set up this app for you.",
        ErrorUnavailable = "This video can't be downloaded: it may be private, removed, age-restricted, or not started yet.",
        ErrorInvalidUrl = "This doesn't look like a YouTube video link.",
        ErrorNetwork = "Can't reach YouTube. Check your internet connection.",
        ErrorToolMissing = "Some program files are missing ({0}). Download the app again and unpack the whole archive.",
        ErrorBadFolder = "This folder can't be used. Choose another one with Browse...",
        ErrorOutOfRange = "The start time is after the end of the video ({0}).",
        ErrorConvert = "Couldn't cut or convert the audio.",
        ErrorUnknown = "Something went wrong.",
    };

    public static UiText Russian { get; } = new()
    {
        WindowTitle = "YouTube Audio Downloader",
        UrlLabel = "Ссылка на YouTube",
        StartLabel = "Начало",
        EndLabel = "Конец",
        TimeHint = "например, 1:35 или 95 (секунды)",
        FadeCheckBox = "Плавное появление и затухание, сек:",
        NormalizeCheckBox = "Выровнять громкость",
        OutputFolderLabel = "Папка для сохранения",
        BrowseButton = "Обзор...",
        FolderDialogDescription = "Куда сохранять MP3-файлы",
        DownloadButton = "Скачать",
        CancelButton = "Отмена",
        OpenFolderButton = "Открыть папку",
        StatusReady = "Готово к работе.",
        StatusUpdating = "Обновление yt-dlp...",
        StatusDownloading = "Скачивание аудио...",
        StatusConverting = "Обрезка и конвертация...",
        StatusDone = "Сохранено: {0}",
        StatusCancelled = "Отменено.",
        StatusError = "Ошибка.",
        ToolChecking = "yt-dlp: проверка обновлений...",
        ToolOk = "yt-dlp {0}",
        ToolUpdateFailed = "yt-dlp {0} · не удалось проверить обновления",
        ToolMissing = "yt-dlp не найден",
        ErrorDialogTitle = "Не удалось сохранить фрагмент",
        CopyDetailsButton = "Скопировать подробности",
        Copied = "Скопировано",
        DetailsExpander = "Подробности",
        ErrorNoUrl = "Сначала вставьте ссылку на YouTube.",
        ErrorBadStart = "Не удалось прочитать время начала. Примеры: 1:35 или 95.",
        ErrorBadEnd = "Не удалось прочитать время конца. Примеры: 2:05 или 125.",
        ErrorEndBeforeStart = "Конец должен быть позже начала.",
        ErrorBlocked = "YouTube заблокировал скачивание. Попробуйте ещё раз через несколько минут. Если ошибка повторяется, нажмите «Скопировать подробности» и отправьте текст тому, кто установил вам эту программу.",
        ErrorUnavailable = "Это видео нельзя скачать: возможно, оно закрыто, удалено, с возрастным ограничением или ещё не началось.",
        ErrorInvalidUrl = "Это не похоже на ссылку на видео YouTube.",
        ErrorNetwork = "Нет связи с YouTube. Проверьте подключение к интернету.",
        ErrorToolMissing = "Не хватает файлов программы ({0}). Скачайте программу заново и распакуйте архив целиком.",
        ErrorBadFolder = "Эту папку нельзя использовать. Выберите другую кнопкой «Обзор...».",
        ErrorOutOfRange = "Время начала больше длины видео ({0}).",
        ErrorConvert = "Не удалось обрезать или сконвертировать аудио.",
        ErrorUnknown = "Что-то пошло не так.",
    };

    // YTAUDIO_LANG=ru|en overrides the Windows display language (useful for screenshots and testing).
    public static UiText Current { get; } = ForLanguage(
        Environment.GetEnvironmentVariable("YTAUDIO_LANG") is { Length: > 0 } language
            ? language
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static UiText ForLanguage(string twoLetterCode) =>
        string.Equals(twoLetterCode, "ru", StringComparison.OrdinalIgnoreCase) ? Russian : English;
}
