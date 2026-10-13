using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gaze.Core;
using CaptureMode = Gaze.Core.CaptureMode;
using Wpf.Ui.Controls;

namespace Gaze.Fluent;

public partial class PanelWindow : FluentWindow
{
    private readonly Func<PanelOptions> getOptions;
    private readonly Action<PanelOptions> apply;
    private readonly Func<bool> readSignInStartup;
    private readonly Action action, cancel, folder, quit, cleanup;
    private readonly Action<string> open, copy;
    private bool editing, updating, idle = true, disposing;
    private uint modifiers; private int key;
    public sealed record Recording(string Path, string Name, string Details);
    public PanelWindow(Func<PanelOptions> getOptions, Action<PanelOptions> apply, Action action, Action cancel, Action<string> open, Action folder, Action<string> copy, Action quit, Action cleanup, Func<bool>? readSignInStartup = null)
    {
        this.readSignInStartup = readSignInStartup ?? (() => false);
        this.getOptions = getOptions; this.apply = apply; this.action = action; this.cancel = cancel; this.open = open; this.folder = folder; this.copy = copy; this.quit = quit; this.cleanup = cleanup;
        EnsureHostApplication();
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(Color.FromRgb(37, 99, 235), Wpf.Ui.Appearance.ApplicationTheme.Light, false);
        InitializeComponent();
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
        Loaded += (_, _) => { if (editing) EditSettings(); };
        updating = true; Mode.ItemsSource = new[] { "Selected window", "All monitors", "Main monitor" }; Mode.SelectedIndex = (int)getOptions().Mode; updating = false;
        Closing += (_, e) => { if (disposing) return; e.Cancel = true; Hide(); Home(); };
        RefreshRecordings();
    }
    public nint NativeHandle => new WindowInteropHelper(this).EnsureHandle();
    private static System.Windows.Application EnsureHostApplication()
    {
        if (System.Windows.Application.Current is { } current) return current;
        // Keep the tray panel independent of GPU/DWM surface state and capture affinity changes.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        return app;
    }
    public static void RunHostDispatcher() => EnsureHostApplication().Run();
    public static void QueueOnHost(Action action) => EnsureHostApplication().Dispatcher.BeginInvoke(action);
    public static void StopHostDispatcher() => System.Windows.Application.Current.Shutdown();
    public object LayoutEvidence() => new { ActualWidth, ActualHeight, IsLoaded, IsVisible, ContentWidth = ((FrameworkElement)Content).ActualWidth, ContentHeight = ((FrameworkElement)Content).ActualHeight };
    public void DisposePanel() { disposing = true; Close(); }
    public void RefreshRecordings()
    {
        string? selected = (Recordings.SelectedItem as Recording)?.Path;
        try
        {
            var directory = getOptions().Folder;
            var files = Directory.Exists(directory) ? new DirectoryInfo(directory).GetFiles("*.mp4").OrderByDescending(f => f.LastWriteTimeUtc).Select(f => new Recording(f.FullName, f.Name, $"{f.LastWriteTime:dd MMM yyyy · HH:mm}   ·   {f.Length / 1048576.0:0.0} MB")).ToArray() : [];
            Recordings.ItemsSource = files; FileCount.Text = files.Length.ToString(); EmptyList.Visibility = files.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Recordings.SelectedItem = files.FirstOrDefault(f => f.Path == selected) ?? files.FirstOrDefault();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { EmptyList.Text = error.Message; EmptyList.Visibility = Visibility.Visible; Recordings.ItemsSource = null; }
        SelectionState();
    }
    private void SelectionState() { bool selected = Recordings.SelectedItem is Recording; OpenFile.IsEnabled = selected; CopyFile.IsEnabled = selected; }
    public void RefreshState(SessionState state, TimeSpan duration, bool busy)
    {
        idle = state == SessionState.Idle && !busy;
        Status.Text = "● " + (busy ? "Starting…" : idle ? "Ready" : state.ToString()); Duration.Text = $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        Status.Foreground = new SolidColorBrush(state is SessionState.Recording or SessionState.Paused ? Color.FromRgb(185, 28, 28) : state is SessionState.Armed or SessionState.Saving ? Color.FromRgb(180, 100, 0) : Color.FromRgb(21, 128, 61));
        MainAction.Content = state switch { SessionState.Idle => "Arm recording", SessionState.Armed => "Start recording", SessionState.Saving => "Saving…", _ => "Stop and save" };
        MainAction.IsEnabled = !busy && state != SessionState.Saving; Mode.IsEnabled = idle; Cleanup.IsEnabled = idle;
        Gear.IsEnabled = idle; CancelArm.Visibility = state == SessionState.Armed ? Visibility.Visible : Visibility.Collapsed; CancelArm.IsEnabled = !busy;
        if (editing && !idle) Home(); SelectionState();
    }
    public void Home()
    {
        editing = false; Heading.Text = "Gaze"; GearIcon.Symbol = SymbolRegular.Settings24;
        Height = 540; PanelFrame.Height = 635.2941176;
        FooterRow.Height = new GridLength(61.7647);
        HomePage.Visibility = HomeFooter.Visibility = Visibility.Visible; SettingsPage.Visibility = SettingsFooter.Visibility = Visibility.Collapsed;
        updating = true; Mode.SelectedIndex = (int)getOptions().Mode; updating = false; RefreshRecordings();
    }
    public void EditSettings()
    {
        if (!idle) return;
        var startupEnabled = readSignInStartup();
        Height = 540; PanelFrame.Height = 635.2941176;
        editing = true; Heading.Text = "Settings"; GearIcon.Symbol = SymbolRegular.ArrowLeft24;
        FooterRow.Height = new GridLength(55.7647);
        HomePage.Visibility = HomeFooter.Visibility = Visibility.Collapsed; SettingsPage.Visibility = SettingsFooter.Visibility = Visibility.Visible;
        var options = getOptions(); Folder.Text = options.Folder; Shortcut.Text = options.ShortcutText; modifiers = options.Modifiers; key = options.Key;
        AutoCopy.IsChecked = false; Clicks.IsChecked = false; DoubleEscape.IsChecked = false;
        AutoCopy.IsChecked = options.AutoCopy; Clicks.IsChecked = options.ClickHighlight; DoubleEscape.IsChecked = options.DoubleEscape;
        Fps.Value = options.FrameRate; Canvas.SelectedIndex = (int)options.Canvas; Canvas.IsEnabled = options.Mode == CaptureMode.Window;
        Compression.SelectedIndex = (int)options.Compression;
        SplitAt29Mb.IsChecked = false; SplitAt29Mb.IsChecked = options.SplitAt29Mb;
        KeyboardTimeline.IsChecked = options.KeyboardTimeline;
        StartAtSignIn.IsChecked = startupEnabled;
    }
    public void RunPreview(string path)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) => { timer.Stop(); Render(path); File.WriteAllText(path + ".state.json", System.Text.Json.JsonSerializer.Serialize(new { AutoCopy = AutoCopy.IsChecked, SplitAt29Mb = SplitAt29Mb.IsChecked, ClickHighlight = Clicks.IsChecked, DoubleEscape = DoubleEscape.IsChecked, Expected = getOptions() })); disposing = true; System.Windows.Application.Current.Shutdown(); };
        timer.Start(); System.Windows.Application.Current.Run(this);
    }
    public void CheckCompression()
    {
        foreach (var mode in new[] { CompressionMode.Compact, CompressionMode.Original, CompressionMode.Compact })
        {
            EditSettings(); Compression.SelectedIndex = (int)mode;
            SaveClick(this, new RoutedEventArgs());
            if (getOptions().Compression != mode) throw new InvalidOperationException("Panel compression save failed.");
            EditSettings();
            if (Compression.SelectedIndex != (int)mode) throw new InvalidOperationException("Panel compression reload failed.");
        }
        Compression.SelectedIndex = (int)CompressionMode.Original;
        CancelSettingsClick(this, new RoutedEventArgs()); EditSettings();
        if (getOptions().Compression != CompressionMode.Compact || Compression.SelectedIndex != (int)CompressionMode.Compact)
            throw new InvalidOperationException("Cancel changed compression.");
    }
    public void CheckSplitting()
    {
        EditSettings();
        var toggle = FindName("SplitAt29Mb") as Wpf.Ui.Controls.ToggleSwitch
            ?? throw new InvalidOperationException("Split recordings over 29 MB toggle is missing from the actual settings panel.");
        var property = typeof(PanelOptions).GetProperty("SplitAt29Mb")
            ?? throw new InvalidOperationException("Split setting is missing from panel options.");
        foreach (var enabled in new[] { true, false, true })
        {
            EditSettings(); toggle.IsChecked = enabled;
            SaveClick(this, new RoutedEventArgs());
            if ((bool)property.GetValue(getOptions())! != enabled) throw new InvalidOperationException("Panel split setting save failed.");
            EditSettings();
            if (toggle.IsChecked != enabled) throw new InvalidOperationException("Panel split setting reload failed.");
        }
        toggle.IsChecked = false; CancelSettingsClick(this, new RoutedEventArgs()); EditSettings();
        if ((bool)property.GetValue(getOptions())! != true || toggle.IsChecked != true)
            throw new InvalidOperationException("Cancel changed the split setting.");
    }
    public void CheckKeyboard()
    {
        EditSettings();
        if (getOptions().KeyboardTimeline || KeyboardTimeline.IsChecked == true) throw new InvalidOperationException("Keyboard capture must default off.");
        foreach (bool enabled in new[] { true, false })
        {
            KeyboardTimeline.IsChecked = enabled; SaveClick(this, new RoutedEventArgs()); EditSettings();
            if (getOptions().KeyboardTimeline != enabled || KeyboardTimeline.IsChecked != enabled) throw new InvalidOperationException("Keyboard setting save/reload failed.");
        }
        KeyboardTimeline.IsChecked = true; CancelSettingsClick(this, new RoutedEventArgs()); EditSettings();
        if (getOptions().KeyboardTimeline || KeyboardTimeline.IsChecked == true) throw new InvalidOperationException("Keyboard cancel changed saved setting.");
    }
    public void Render(string path)
    {
        var content = (FrameworkElement)Content; content.Measure(new Size(Width, Height)); content.Arrange(new Rect(0, 0, Width, Height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
    }
    private void GearClick(object sender, RoutedEventArgs e)
    {
        try { if (editing) Home(); else EditSettings(); }
        catch (Exception error) { System.Windows.MessageBox.Show(this, error.Message, "Could not read Windows sign-in startup"); }
    }
    private void DismissClick(object sender, RoutedEventArgs e) { Hide(); Home(); }
    private void MainClick(object sender, RoutedEventArgs e) { Hide(); action(); }
    private void CancelClick(object sender, RoutedEventArgs e) => cancel();
    private void SelectedRecordingChanged(object sender, SelectionChangedEventArgs e) { if (IsInitialized) SelectionState(); }
    private void RecordingDoubleClick(object sender, MouseButtonEventArgs e) { if (Recordings.SelectedItem is Recording file) open(file.Path); }
    private void OpenClick(object sender, RoutedEventArgs e) { if (Recordings.SelectedItem is Recording file) open(file.Path); }
    private void CopyClick(object sender, RoutedEventArgs e) { if (Recordings.SelectedItem is Recording file) copy(file.Path); }
    private void CleanupClick(object sender, RoutedEventArgs e) { cleanup(); RefreshRecordings(); }
    private void FolderClick(object sender, RoutedEventArgs e) => folder();
    private void QuitClick(object sender, RoutedEventArgs e) => quit();
    private void CancelSettingsClick(object sender, RoutedEventArgs e) => Home();
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || !IsInitialized) return;
        var options = getOptions(); options.Mode = (CaptureMode)Mode.SelectedIndex;
        try { apply(options); } catch (Exception error) { System.Windows.MessageBox.Show(this, error.Message, "Could not change capture mode"); updating = true; Mode.SelectedIndex = (int)getOptions().Mode; updating = false; }
    }
    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = Folder.Text, Title = "Recording folder" };
        if (picker.ShowDialog(this) == true) Folder.Text = picker.FolderName;
    }
    private void ShortcutKey(object sender, KeyEventArgs e)
    {
        e.Handled = true; var pressed = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (pressed is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift) return;
        key = KeyInterop.VirtualKeyFromKey(pressed); var mods = Keyboard.Modifiers;
        modifiers = (uint)((mods.HasFlag(ModifierKeys.Control) ? 2 : 0) | (mods.HasFlag(ModifierKeys.Alt) ? 1 : 0) | (mods.HasFlag(ModifierKeys.Shift) ? 4 : 0));
        Shortcut.Text = $"{(mods.HasFlag(ModifierKeys.Control) ? "Ctrl + " : "")}{(mods.HasFlag(ModifierKeys.Alt) ? "Alt + " : "")}{(mods.HasFlag(ModifierKeys.Shift) ? "Shift + " : "")}{pressed}";
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (modifiers == 0 || key is 27 or 123) throw new InvalidOperationException("Use a modified shortcut other than Esc or F12.");
            var fps = Fps.Value ?? double.NaN;
            if (fps < 1 || fps > 30 || double.IsNaN(fps) || fps != Math.Truncate(fps)) throw new InvalidOperationException("Frames per second must be a whole number from 1 to 30.");
            var options = getOptions(); options.Folder = Path.GetFullPath(Folder.Text); options.Modifiers = modifiers; options.Key = key; options.FrameRate = (int)fps;
            options.AutoCopy = AutoCopy.IsChecked == true; options.ClickHighlight = Clicks.IsChecked == true; options.DoubleEscape = DoubleEscape.IsChecked == true; options.Canvas = (WindowCanvasMode)Canvas.SelectedIndex;
            if (Compression.SelectedIndex < 0) throw new InvalidOperationException("Choose a compression mode.");
            options.Compression = (CompressionMode)Compression.SelectedIndex;
            options.SplitAt29Mb = SplitAt29Mb.IsChecked == true;
            options.KeyboardTimeline = KeyboardTimeline.IsChecked == true;
            options.StartAtSignIn = StartAtSignIn.IsChecked == true;
            apply(options); Home();
        }
        catch (Exception error) { System.Windows.MessageBox.Show(this, error.Message, "Could not save settings"); }
    }
}
