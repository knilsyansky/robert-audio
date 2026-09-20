namespace YtAudioDownloader;

// The window is laid out in code (no designer file) so it can grow for longer Russian texts.
public partial class MainForm
{
    private const int ContentWidth = 520;

    private readonly TextBox urlTextBox = new() { Width = ContentWidth, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox startTextBox = new() { Width = 90, Text = "0:00" };
    private readonly TextBox endTextBox = new() { Width = 90, Text = "0:30" };
    private readonly CheckBox fadeCheckBox = new() { AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };
    private readonly NumericUpDown fadeSecondsUpDown = new()
    {
        Width = 60, Minimum = 0.5m, Maximum = 10m, Increment = 0.5m, DecimalPlaces = 1, Value = 3m,
    };
    private readonly CheckBox normalizeCheckBox = new() { AutoSize = true, Checked = true };
    private readonly TextBox outputFolderTextBox = new() { Dock = DockStyle.Fill };
    private readonly Button browseButton = new() { AutoSize = true };
    private readonly Button downloadButton = new() { AutoSize = true, MinimumSize = new Size(110, 32) };
    private readonly Button cancelButton = new() { AutoSize = true, Enabled = false, MinimumSize = new Size(0, 32) };
    private readonly Button openFolderButton = new() { AutoSize = true, Visible = false, MinimumSize = new Size(0, 32) };
    private readonly ProgressBar progressBar = new()
    {
        Width = ContentWidth, Height = 18, Maximum = 1000, Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(ContentWidth, 0) };
    private readonly Label toolStatusLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    private void InitializeLayout()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Font = SystemFonts.MessageBoxFont ?? Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };

        root.Controls.Add(Caption(_ui.UrlLabel));
        root.Controls.Add(urlTextBox);

        var times = new TableLayoutPanel { ColumnCount = 3, RowCount = 2, AutoSize = true, Margin = Padding.Empty };
        times.Controls.Add(Caption(_ui.StartLabel), 0, 0);
        times.Controls.Add(Caption(_ui.EndLabel), 1, 0);
        times.Controls.Add(startTextBox, 0, 1);
        times.Controls.Add(endTextBox, 1, 1);
        times.Controls.Add(new Label { Text = _ui.TimeHint, AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 2, 1);
        root.Controls.Add(times);

        var fadeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        fadeCheckBox.Text = _ui.FadeCheckBox;
        fadeRow.Controls.Add(fadeCheckBox);
        fadeRow.Controls.Add(fadeSecondsUpDown);
        root.Controls.Add(fadeRow);

        normalizeCheckBox.Text = _ui.NormalizeCheckBox;
        root.Controls.Add(normalizeCheckBox);

        root.Controls.Add(Caption(_ui.OutputFolderLabel));
        var outputRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        browseButton.Text = _ui.BrowseButton;
        outputRow.Controls.Add(outputFolderTextBox, 0, 0);
        outputRow.Controls.Add(browseButton, 1, 0);
        root.Controls.Add(outputRow);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 4) };
        downloadButton.Text = _ui.DownloadButton;
        cancelButton.Text = _ui.CancelButton;
        openFolderButton.Text = _ui.OpenFolderButton;
        actions.Controls.AddRange(new Control[] { downloadButton, cancelButton, openFolderButton });
        root.Controls.Add(actions);

        root.Controls.Add(progressBar);
        statusLabel.Text = _ui.StatusReady;
        root.Controls.Add(statusLabel);
        root.Controls.Add(toolStatusLabel);

        Controls.Add(root);
        AcceptButton = downloadButton;

        urlTextBox.TextChanged += urlTextBox_TextChanged;
        startTextBox.TextChanged += startTextBox_TextChanged;
        endTextBox.TextChanged += endTextBox_TextChanged;
        fadeCheckBox.CheckedChanged += (_, _) => fadeSecondsUpDown.Enabled = fadeCheckBox.Checked;
        browseButton.Click += browseButton_Click;
        downloadButton.Click += downloadButton_Click;
        cancelButton.Click += (_, _) => _cancellation?.Cancel();
        openFolderButton.Click += openFolderButton_Click;

        ResumeLayout(performLayout: true);
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
}
