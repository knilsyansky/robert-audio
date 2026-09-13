using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YtAudioDownloader
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            outputTextBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output.mp3");
        }

        private async void downloadButton_Click(object sender, EventArgs e)
        {
            SetUiEnabled(false);
            statusLabel.Text = "Processing...";
            string tempDir = string.Empty;

            try
            {
                string url = urlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(url))
                    throw new InvalidOperationException("Please enter a YouTube URL.");

                if (!TimeSpan.TryParse(startTextBox.Text.Trim(), out TimeSpan startTime))
                    throw new InvalidOperationException("Start time must be a valid time span (e.g. 00:01:30).");

                if (!TimeSpan.TryParse(endTextBox.Text.Trim(), out TimeSpan endTime))
                    throw new InvalidOperationException("End time must be a valid time span (e.g. 00:02:30).");

                if (endTime <= startTime)
                    throw new InvalidOperationException("End time must be greater than start time.");

                string outputPath = outputTextBox.Text.Trim();
                if (string.IsNullOrEmpty(outputPath))
                    throw new InvalidOperationException("Please select an output file.");

                string ytDlpPath = ResolveToolPath("yt-dlp.exe");
                string ffmpegPath = ResolveToolPath("ffmpeg.exe");

                string tempRoot = Path.Combine(Path.GetTempPath(), "YtAudioDownloader");
                Directory.CreateDirectory(tempRoot);
                tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                string downloadedFile = await DownloadAudioAsync(ytDlpPath, url, tempDir);
                string filter = BuildAudioFilter(endTime - startTime);
                await ConvertAudioAsync(ffmpegPath, downloadedFile, outputPath, startTime, endTime, filter);

                statusLabel.Text = "Finished successfully.";
                MessageBox.Show("Audio saved to: " + outputPath, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiEnabled(true);
                if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup failures.
                    }
                }
            }
        }

        private static string ResolveToolPath(string toolName)
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, toolName);
            if (File.Exists(localPath))
                return localPath;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string path in pathEnv.Split(Path.PathSeparator))
            {
                string candidate = Path.Combine(path.Trim(), toolName);
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException($"Could not find {toolName}. Place it in the application folder or add it to PATH.");
        }

        private async Task<string> DownloadAudioAsync(string ytDlpPath, string url, string tempDir)
        {
            string outputPattern = Path.Combine(tempDir, "download.%(ext)s");
            string arguments = $"--no-playlist --no-part -f bestaudio[ext=m4a]/bestaudio -o \"{outputPattern}\" \"{url}\"";
            await RunProcessAsync(ytDlpPath, arguments, "Downloading audio...");

            string? downloadedFile = FindDownloadedFile(tempDir);
            if (downloadedFile == null)
                throw new FileNotFoundException("Downloaded audio file not found.");

            FileInfo fileInfo = new FileInfo(downloadedFile);
            if (fileInfo.Length < 1024)
                throw new InvalidOperationException($"Downloaded audio file is too small ({fileInfo.Length} bytes). The download may have failed.");

            return downloadedFile;
        }

        private static string? FindDownloadedFile(string tempDir)
        {
            string? bestFile = null;
            long bestSize = 0;

            foreach (string file in Directory.GetFiles(tempDir, "download.*"))
            {
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".part", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo info = new FileInfo(file);
                if (info.Length > bestSize)
                {
                    bestSize = info.Length;
                    bestFile = file;
                }
            }

            return bestFile;
        }

        private static string BuildAudioFilter(TimeSpan duration)
        {
            double fadeSeconds = 3.0;
            double fadeOutStart = Math.Max(0, duration.TotalSeconds - fadeSeconds);
            return $"afade=t=in:st=0:d={fadeSeconds},afade=t=out:st={fadeOutStart}:d={fadeSeconds}";
        }

        private async Task ConvertAudioAsync(string ffmpegPath, string inputFile, string outputPath, TimeSpan startTime, TimeSpan endTime, string filter)
        {
            string startArg = FormatTimeSpan(startTime);
            string durationArg = FormatTimeSpan(endTime - startTime);
            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            string arguments = $"-y -ss {startArg} -i \"{inputFile}\" -t {durationArg} -af \"{filter}\" -vn -c:a libmp3lame -q:a 2 \"{outputPath}\"";
            await RunProcessAsync(ffmpegPath, arguments, "Converting audio...");
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        private async Task RunProcessAsync(string fileName, string arguments, string statusMessage)
        {
            statusLabel.Text = statusMessage;
            using Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Command failed: {fileName} {arguments}\n{error}");
        }

        private void SetUiEnabled(bool enabled)
        {
            urlTextBox.Enabled = enabled;
            startTextBox.Enabled = enabled;
            endTextBox.Enabled = enabled;
            outputTextBox.Enabled = enabled;
            browseButton.Enabled = enabled;
            downloadButton.Enabled = enabled;
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            using SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "MP3 files (*.mp3)|*.mp3";
            dialog.FileName = "output.mp3";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                outputTextBox.Text = dialog.FileName;
            }
        }
    }
}
