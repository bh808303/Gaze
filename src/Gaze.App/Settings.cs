using System.Text.Json;
using Gaze.Core;

namespace Gaze.App;

internal sealed class Settings
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? StartAtSignIn { get; set; }
    public TranscribeTrigger TranscribeTrigger { get; set; } = TranscribeTrigger.MiddleMouse;
    public CaptureMode Mode { get; set; }
    public WindowCanvasMode WindowCanvas { get; set; } = WindowCanvasMode.OriginalWindow;
    public int FrameRate { get; set; } = 5;
    public CompressionMode Compression { get; set; } = CompressionMode.Original;
    public bool ClickHighlight { get; set; } = true;
    public bool AutoCopy { get; set; } = true;
    public bool SplitAt29Mb { get; set; }
    public bool KeyboardTimeline { get; set; }
    public bool RecordTimelineMetadata { get; set; }
    public bool DoubleEscape { get; set; } = true;
    public uint Modifiers { get; set; } = 3;
    public Keys Key { get; set; } = Keys.PrintScreen;
    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Gaze");
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gaze", "settings.json");
    internal static Settings Load(string? path = null)
    {
        path ??= PathName;
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new();
        if (settings.FrameRate is < 1 or > 30 || string.IsNullOrWhiteSpace(settings.Folder) || !Enum.IsDefined(settings.TranscribeTrigger) || !Enum.IsDefined(settings.Mode) || !Enum.IsDefined(settings.WindowCanvas) || !Enum.IsDefined(settings.Compression))
            throw new InvalidDataException($"Invalid settings in {path}.");
        return settings;
    }
    internal void Save(string? path = null)
    {
        path ??= PathName;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
    internal TimelineCollectionOptions CollectionOptions => new() { RecordTimelineMetadata = RecordTimelineMetadata, KeyboardTimeline = KeyboardTimeline };
    internal string Shortcut => $"{((Modifiers & 2) != 0 ? "Ctrl + " : "")}{((Modifiers & 1) != 0 ? "Alt + " : "")}{((Modifiers & 4) != 0 ? "Shift + " : "")}{Key}";
}

internal sealed class SettingsForm : Form
{
    internal SettingsForm(Settings settings, Action<Settings> apply)
    {
        Text = "Gaze · Settings"; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 580); MinimumSize = new Size(790, 530);
        MaximizeBox = false; MinimizeBox = false; BackColor = Color.FromArgb(245, 246, 248);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(20, 14, 20, 14), FlowDirection = FlowDirection.RightToLeft, BackColor = Color.White };
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 14, 0) };
        sidebar.Controls.Add(new Label { Text = "Gaze", AutoSize = true, Font = new Font("Segoe UI", 19F, FontStyle.Bold), Margin = new Padding(10, 0, 0, 26) });
        var host = new Panel { Dock = DockStyle.Fill };
        shell.Controls.Add(sidebar, 0, 0); shell.Controls.Add(host, 1, 0);
        var pages = new List<(Panel Page, Button Navigation)>();
        TableLayoutPanel layout = null!;
        int rowIndex = 0;
        void Wide(Control control)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 4, 0, 12);
            layout.Controls.Add(control, 0, rowIndex++);
        }
        void ShowPage(Panel page)
        {
            foreach (var entry in pages)
            {
                bool selected = entry.Page == page;
                entry.Page.Visible = selected;
                entry.Navigation.BackColor = selected ? Color.FromArgb(225, 232, 239) : BackColor;
                entry.Navigation.ForeColor = selected ? Color.FromArgb(0, 92, 160) : Color.FromArgb(45, 48, 54);
            }
            page.BringToFront();
        }
        void Section(string title, string description)
        {
            var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14, 4, 8, 12), Visible = false };
            layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rowIndex = 0;
            page.Controls.Add(layout); host.Controls.Add(page);
            var navigation = new Button { Text = title, TextAlign = ContentAlignment.MiddleLeft, Width = 158, Height = 43, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 0, 6), Padding = new Padding(12, 0, 0, 0), AccessibleName = title + " settings" };
            navigation.FlatAppearance.BorderSize = 0;
            navigation.Click += (_, _) => ShowPage(page);
            sidebar.Controls.Add(navigation); pages.Add((page, navigation));
            Wide(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 20F, FontStyle.Bold) });
            Wide(new Label { Text = description, AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(80, 85, 95) });
        }
        void Row(string label, Control control)
        {
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 8) };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            control.Margin = new Padding(0, 2, 0, 2); control.AccessibleName = label;
            card.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 5, 10, 0) }, 0, 0);
            card.Controls.Add(control, 1, 0);
            Wide(card);
        }
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        mode.Items.AddRange(["Selected window", "All monitors", "Main monitor", "Screenshot"]); mode.SelectedIndex = (int)settings.Mode;
        var trigger = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        trigger.Items.AddRange(["Middle mouse", "Print Screen"]); trigger.SelectedIndex = (int)settings.TranscribeTrigger;
        var canvas = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = settings.Mode == CaptureMode.Window };
        canvas.Items.AddRange(["Original window size (default)", "Selected window's monitor size"]);
        canvas.SelectedIndex = (int)settings.WindowCanvas;
        mode.SelectedIndexChanged += (_, _) => canvas.Enabled = mode.SelectedIndex == (int)CaptureMode.Window;
        var fps = new NumericUpDown { Minimum = 1, Maximum = 30, Value = settings.FrameRate };
        var compression = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        compression.Items.AddRange(["Original — sharper image", "Compact — smaller file"]);
        compression.SelectedIndex = (int)settings.Compression;
        var folder = new TextBox { Text = settings.Folder, Dock = DockStyle.Fill };
        var key = new TextBox { Text = settings.Shortcut, ReadOnly = true, Dock = DockStyle.Fill };
        uint modifiers = settings.Modifiers; Keys shortcut = settings.Key;
        key.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey) return;
            modifiers = (uint)((e.Control ? 2 : 0) | (e.Alt ? 1 : 0) | (e.Shift ? 4 : 0)); shortcut = e.KeyCode;
            key.Text = new Settings { Modifiers = modifiers, Key = shortcut }.Shortcut;
        };
        var metadata = new CheckBox { Text = "Record timeline metadata", Checked = settings.RecordTimelineMetadata, AutoSize = true };
        var keyboard = new CheckBox { Text = "Keyboard events in timeline", Checked = settings.KeyboardTimeline, AutoSize = true };
        var clicks = new CheckBox { Text = "Highlight mouse clicks", Checked = settings.ClickHighlight, AutoSize = true };
        var copy = new CheckBox { Text = "Copy saved files to clipboard", Checked = settings.AutoCopy, AutoSize = true };
        var split = new CheckBox { Text = "Claude Desktop Clipboard", AccessibleName = "Claude Desktop Clipboard", Checked = settings.SplitAt29Mb, AutoSize = true };
        var escape = new CheckBox { Text = "Double-Esc stops within 1 second", Checked = settings.DoubleEscape, AutoSize = true };
        Section("Capture", "Record a window, your main monitor, or all monitors.");
        Row("Capture mode", mode); Row("Window canvas", canvas);
        Row("Frames per second", fps);
        Row("Compression", compression);
        Wide(new Label { Text = "Compact keeps resolution and frame rate, but may soften small text and uses more CPU. Seeking can take longer. File size varies; uploads under 30 MB are not guaranteed.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(80, 85, 95) });
        Wide(new Label { Text = "5 fps is recommended for readable text and smaller files. Video only; no audio.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(80, 85, 95) });
        Row("Mouse clicks", clicks);
        Row("Timeline metadata", metadata); Row("Keyboard timeline", keyboard);
        Section("Controls", "Click the tray icon or use your shortcut to arm, record, and save.");
        Row("Screenshot trigger", trigger);
        Row("Shortcut", key);
        Wide(new Label { Text = "Select the shortcut field, then press your preferred key combination.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(80, 85, 95) });
        escape.Text = "Press Esc twice within 1 second to stop";
        Row("Stop confirmation", escape);
        Section("Saving", "Choose where recordings go and keep them ready to paste.");
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var browse = new Button { Text = "Browse…", AutoSize = true, AccessibleName = "Browse recording folder" };
        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "Choose a folder for Gaze recordings", UseDescriptionForTitle = true, SelectedPath = folder.Text };
            if (picker.ShowDialog(this) == DialogResult.OK) folder.Text = picker.SelectedPath;
        };
        folderRow.Controls.Add(folder, 0, 0); folderRow.Controls.Add(browse, 1, 0);
        Row("Save folder", folderRow); Row("File size", split);
        split.AccessibleName = "Claude Desktop Clipboard";
        Wide(new Label { Text = "Video: 29 MB parts. Screenshot: 20 images / 29 MB per batch. Video splitting re-encodes; Auto-copy copies all parts.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(80, 85, 95) });
        Row("After saving", copy);
        var save = new Button { Text = "Save settings", AutoSize = true, Padding = new Padding(12, 4, 12, 4), BackColor = Color.FromArgb(0, 103, 184), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        save.FlatAppearance.BorderSize = 0;
        var cancel = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(12, 4, 12, 4), DialogResult = DialogResult.Cancel };
        cancel.Click += (_, _) => Close(); AcceptButton = save; CancelButton = cancel;
        footer.Controls.Add(save); footer.Controls.Add(cancel);
        save.Click += (_, _) =>
        {
            try
            {
                if (modifiers == 0 || shortcut is Keys.Escape or Keys.F12) throw new InvalidOperationException("Use a modified shortcut other than Esc or F12.");
                var proposed = new Settings { TranscribeTrigger = (TranscribeTrigger)trigger.SelectedIndex, Mode = (CaptureMode)mode.SelectedIndex, WindowCanvas = (WindowCanvasMode)canvas.SelectedIndex, FrameRate = (int)fps.Value,
                    Compression = (CompressionMode)compression.SelectedIndex,
                    Folder = Path.GetFullPath(folder.Text), Modifiers = modifiers, Key = shortcut,
                    RecordTimelineMetadata = metadata.Checked, KeyboardTimeline = keyboard.Checked, ClickHighlight = clicks.Checked, AutoCopy = copy.Checked, SplitAt29Mb = split.Checked, DoubleEscape = escape.Checked };
                apply(proposed); Close();
            }
            catch (Exception e) { MessageBox.Show(e.Message, "Could not save settings"); }
        };
        ShowPage(pages[0].Page); Controls.Add(shell); Controls.Add(footer);
    }
}
