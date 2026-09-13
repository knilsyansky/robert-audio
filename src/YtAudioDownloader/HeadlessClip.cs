using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace YtAudioDownloader;

// "YtAudioDownloader.exe --clip <url> <start> <end> <output folder>": the full pipeline without the window,
// used to smoke-test a packaged build. Prints "OK <path>" or "ERROR ..." to stdout.
internal static class HeadlessClip
{
    public static int Run(string[] args)
    {
        // The GUI exe has no console of its own, so Console.OutputEncoding can't be set; redirecting
        // stdout still needs UTF-8 so Cyrillic titles/paths in "OK <path>" come out readable.
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });

        if (!TryParseArguments(args, out ClipRequest? request, out string? error))
        {
            Console.WriteLine("ERROR " + error);
            return 2;
        }

        try
        {
            YtDlpClient ytDlp = YtDlpClient.CreateDefault();
            ytDlp.EnsureReadyAsync(CancellationToken.None).GetAwaiter().GetResult();
            UpdateOutcome update = ytDlp.UpdateAsync(CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"yt-dlp {update.Version} (update {(update.Succeeded ? "ok" : "failed")})");

            ClipResult result = new ClipService(ytDlp, FfmpegClient.CreateDefault())
                .CreateClipAsync(request, null, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("OK " + result.OutputPath);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Write("Headless clip failed: " + ex);
            Console.WriteLine("ERROR " + ex.Message + (ex is YtDlpException ytDlpError ? Environment.NewLine + ytDlpError.Details : ""));
            return 1;
        }
    }

    public static bool TryParseArguments(string[] args, [NotNullWhen(true)] out ClipRequest? request, [NotNullWhen(false)] out string? error)
    {
        request = null;
        if (args.Length != 4)
        {
            error = "usage: --clip <url> <start> <end> <output folder>";
            return false;
        }
        if (!TimeInput.TryParse(args[1], out TimeSpan start))
        {
            error = "bad start time: " + args[1];
            return false;
        }
        if (!TimeInput.TryParse(args[2], out TimeSpan end) || end <= start)
        {
            error = "bad end time: " + args[2];
            return false;
        }

        error = null;
        request = new ClipRequest(args[0], start, end, Fade: true, FadeSeconds: 3, Normalize: true, OutputFolder: args[3]);
        return true;
    }
}
