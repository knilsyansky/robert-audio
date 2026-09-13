using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YtAudioDownloader;

internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

internal static class ProcessRunner
{
    // Runs a console tool without a window, streams stdout line by line, and kills it (with children) on cancellation.
    public static async Task<ProcessResult> RunAsync(
        string fileName, IEnumerable<string> arguments, Action<string>? onStdOutLine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        Log.Write("RUN " + Path.GetFileName(fileName) + " " + string.Join(" ", startInfo.ArgumentList.Select(QuoteForLog)));

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        Task pumpOut = PumpAsync(process.StandardOutput, line =>
        {
            stdout.AppendLine(line);
            onStdOutLine?.Invoke(line);
        });
        Task pumpErr = PumpAsync(process.StandardError, line => stderr.AppendLine(line));

        using (cancellationToken.Register(() => Kill(process)))
        {
            await Task.WhenAll(pumpOut, pumpErr).ConfigureAwait(false);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Log.Write("CANCELLED " + Path.GetFileName(fileName));
            cancellationToken.ThrowIfCancellationRequested();
        }

        Log.Write($"EXIT {process.ExitCode} {Path.GetFileName(fileName)}" + (stderr.Length > 0 ? Environment.NewLine + stderr : ""));
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is string line)
            onLine(line);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }

    private static string QuoteForLog(string argument) => argument.Contains(' ') ? "\"" + argument + "\"" : argument;
}
