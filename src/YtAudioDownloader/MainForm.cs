using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace YtAudioDownloader;

public partial class MainForm : Form
{
    private static readonly TimeSpan DefaultClipLength = TimeSpan.FromSeconds(30);

    private readonly UiText _ui = Strings.Current;
    private readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    private readonly YtDlpClient _ytDlp = YtDlpClient.CreateDefault();
    private readonly ClipService _clips;
    private Task _ytDlpReady = Task.CompletedTask;
    private string _ytDlpVersion = "?";
    private CancellationTokenSource? _cancellation;
    private string? _lastOutputPath;
    private bool _endEditedByUser;
    private bool _settingEnd;
    private bool _closeWhenIdle;

    public MainForm()
    {
        InitializeLayout();
        _clips = new ClipService(_ytDlp, FfmpegClient.CreateDefault());
        Text = $"{_ui.WindowTitle} {Application.ProductVersion}";
        outputFolderTextBox.Text = string.IsNullOrWhiteSpace(_settings.OutputFolder) ? AppPaths.DefaultOutputDir : _settings.OutputFolder;
        Log.DeleteOldFiles();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ytDlpReady = PrepareYtDlpAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cancellation != null)
        {
            // Let the running clip delete its partial file and temp folder before the app exits.
            e.Cancel = true;
            _closeWhenIdle = true;
            _cancellation.Cancel();
            return;
        }
        base.OnFormClosing(e);
    }

    // Startup: make sure yt-dlp works, then update it in the background. Never throws.
    private async Task PrepareYtDlpAsync()
    {
        toolStatusLabel.Text = _ui.ToolChecking;
        try
        {
            _ytDlpVersion = await _ytDlp.EnsureReadyAsync(CancellationToken.None);
            UpdateOutcome update = await _ytDlp.UpdateAsync(CancellationToken.None);
            _ytDlpVersion = update.Version ?? _ytDlpVersion;
            if (!IsDisposed)
                toolStatusLabel.Text = string.Format(update.Succeeded ? _ui.ToolOk : _ui.ToolUpdateFailed, _ytDlpVersion);
        }
        catch (ToolMissingException ex)
        {
            Log.Write(ex.Message);
            if (!IsDisposed)
                toolStatusLabel.Text = _ui.ToolMissing;
        }
        catch (Exception ex)
        {
            Log.Write("yt-dlp preparation failed: " + ex);
            if (!IsDisposed)
                toolStatusLabel.Text = string.Format(_ui.ToolUpdateFailed, _ytDlpVersion);
        }
    }

    private void urlTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (YouTubeUrl.TryGetStartTime(urlTextBox.Text) is not TimeSpan start)
            return;
        _endEditedByUser = false; // a link with a timestamp starts a new clip
        startTextBox.Text = TimeInput.FormatDisplay(start);
        SetDefaultEndIfUntouched();
    }

    private void startTextBox_TextChanged(object? sender, EventArgs e) => SetDefaultEndIfUntouched();

    private void endTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_settingEnd)
            _endEditedByUser = true;
    }

    // End follows Start + 30 s until the user types an end of their own.
    private void SetDefaultEndIfUntouched()
    {
        if (_endEditedByUser || !TimeInput.TryParse(startTextBox.Text, out TimeSpan start))
            return;
        _settingEnd = true;
        endTextBox.Text = TimeInput.FormatDisplay(start + DefaultClipLength);
        _settingEnd = false;
    }

    private async void downloadButton_Click(object? sender, EventArgs e)
    {
        if (_cancellation != null || ReadRequest() is not ClipRequest request)
            return;

        _settings.OutputFolder = request.OutputFolder;
        _settings.Save(Settings.DefaultPath);

        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        try
        {
            if (!_ytDlpReady.IsCompleted)
            {
                ShowProgress(new ClipProgress(ClipStage.Updating, null));
                await _ytDlpReady.WaitAsync(_cancellation.Token);
            }

            ClipResult result = await _clips.CreateClipAsync(request, new Progress<ClipProgress>(ShowProgress), _cancellation.Token);
            _lastOutputPath = result.OutputPath;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBar.Maximum;
            statusLabel.Text = string.Format(_ui.StatusDone, Path.GetFileName(result.OutputPath));
            openFolderButton.Visible = true;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                ResetProgress();
                statusLabel.Text = _ui.StatusCancelled;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Clip failed: " + ex);
            if (!IsDisposed)
            {
                ResetProgress();
                statusLabel.Text = _ui.StatusError;
                ShowError(ex, request);
            }
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            if (!IsDisposed)
                SetRunning(false);
            if (_closeWhenIdle)
                BeginInvoke(Close);
        }
    }

    private ClipRequest? ReadRequest()
    {
        string url = urlTextBox.Text.Trim();
        TimeSpan start = TimeSpan.Zero;
        TimeSpan end = TimeSpan.Zero;
        string folder = AppPaths.DefaultOutputDir;
        string? problem = null;

        if (url.Length == 0)
            problem = _ui.ErrorNoUrl;
        else if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            problem = _ui.ErrorInvalidUrl;
        else if (!TimeInput.TryParse(startTextBox.Text, out start))
            problem = _ui.ErrorBadStart;
        else if (!TimeInput.TryParse(endTextBox.Text, out end))
            problem = _ui.ErrorBadEnd;
        else if (end <= start)
            problem = _ui.ErrorEndBeforeStart;
        else
        {
            string requestedFolder = outputFolderTextBox.Text.Trim();
            try
            {
                folder = requestedFolder.Length > 0 ? Path.GetFullPath(requestedFolder) : AppPaths.DefaultOutputDir;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
            {
                problem = _ui.ErrorBadFolder;
            }
        }

        if (problem != null)
        {
            MessageBox.Show(this, problem, _ui.ErrorDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new ClipRequest(url, start, end, fadeCheckBox.Checked, (double)fadeSecondsUpDown.Value, normalizeCheckBox.Checked, folder);
    }

    private void ShowProgress(ClipProgress progress)
    {
        if (IsDisposed)
            return;

        string stage = progress.Stage switch
        {
            ClipStage.Updating => _ui.StatusUpdating,
            ClipStage.Downloading => _ui.StatusDownloading,
            _ => _ui.StatusConverting,
        };
        if (progress.Fraction is double fraction)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = (int)Math.Round(fraction * progressBar.Maximum);
            statusLabel.Text = stage + " " + fraction.ToString("P0", CultureInfo.CurrentCulture);
        }
        else
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = stage;
        }
    }

    private void ResetProgress()
    {
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = 0;
    }

    private void SetRunning(bool running)
    {
        foreach (Control control in new Control[] { urlTextBox, startTextBox, endTextBox, fadeCheckBox, normalizeCheckBox, outputFolderTextBox, browseButton, downloadButton })
            control.Enabled = !running;
        fadeSecondsUpDown.Enabled = !running && fadeCheckBox.Checked;
        cancelButton.Enabled = running;
        if (running)
            openFolderButton.Visible = false;
    }

    private void ShowError(Exception error, ClipRequest request)
    {
        (string message, string details) = ErrorText.Describe(error, _ui);
        string report = string.Join(Environment.NewLine,
            $"App: {Application.ProductVersion}",
            $"yt-dlp: {_ytDlpVersion}",
            $"URL: {request.Url}",
            $"Clip: {TimeInput.FormatDisplay(request.Start)} - {TimeInput.FormatDisplay(request.End)}",
            $"Log: {Log.CurrentFile}",
            "",
            details.Trim());

        var copyButton = new TaskDialogButton(_ui.CopyDetailsButton) { AllowCloseDialog = false };
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(report);
                copyButton.Text = _ui.Copied;
            }
            catch (ExternalException ex)
            {
                Log.Write("Clipboard is busy: " + ex.Message);
            }
        };

        TaskDialog.ShowDialog(this, new TaskDialogPage
        {
            Caption = _ui.WindowTitle,
            Heading = _ui.ErrorDialogTitle,
            Text = message,
            Icon = TaskDialogIcon.Error,
            Buttons = { copyButton, TaskDialogButton.OK },
            Expander = new TaskDialogExpander(report)
            {
                CollapsedButtonText = _ui.DetailsExpander,
                ExpandedButtonText = _ui.DetailsExpander,
            },
        });
    }

    private void browseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = _ui.FolderDialogDescription,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(outputFolderTextBox.Text) ? outputFolderTextBox.Text : AppPaths.DefaultOutputDir,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            outputFolderTextBox.Text = dialog.SelectedPath;
    }

    private void openFolderButton_Click(object? sender, EventArgs e)
    {
        if (_lastOutputPath != null && File.Exists(_lastOutputPath))
            Process.Start("explorer.exe", $"/select,\"{_lastOutputPath}\"")?.Dispose();
    }
}
