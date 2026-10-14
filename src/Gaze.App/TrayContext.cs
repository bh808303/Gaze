using System.Diagnostics;
using System.Runtime.InteropServices;
using Gaze.Core;

namespace Gaze.App;

internal sealed class TrayContext : ApplicationContext
{
    private sealed class MessageWindow : Form
    {
        internal Action? ShortcutPressed;
        protected override void WndProc(ref Message m)
        { if (m.Msg == 0x312) ShortcutPressed?.Invoke(); base.WndProc(ref m); }
    }
    private readonly MessageWindow messages = new();
    private readonly TrayIcons icons = new();
    private readonly NotifyIcon tray = new() { Text = "Gaze · Idle" };
    private readonly Overlay overlay = new();
    private readonly Session session = new();
    private readonly EscapeConfirmation escape = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private readonly Native.HookCallback hookCallback;
    private Settings settings = Settings.Load();
    private Capture? capture;
    private nint hook, current, lastForeground;
    private TimeSpan lastTick, noticeUntil;
    private bool escHeld, starting, quitAfterSave, cleaning;
    private string? lastFile;
    private TrayPanel? panel;
    private int diskTick;
    private TranscribeController? transcribe;
    private readonly List<TranscribeExporter> transcribeExports = [];

    internal TrayContext(bool validation = false)
    {
        tray.Icon = icons.For(SessionState.Idle); tray.Visible = true;
        _ = messages.Handle;
        messages.ShortcutPressed = () => Guard(() => Shortcut());
        if (!validation) RegisterShortcut(settings);
        hookCallback = Keyboard;
        hook = Native.SetWindowsHookEx(13, hookCallback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot install Escape key handler.");
        RememberForeground();
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) Guard(() => Shortcut(fromTray: true));
            else if (e.Button == MouseButtons.Right) Gaze.Fluent.PanelWindow.QueueOnHost(() => Guard(OpenPanel));
        };
        tray.BalloonTipClicked += (_, _) => { if (lastFile != null) OpenFolder(); };
        timer.Tick += (_, _) => Guard(Tick); timer.Start(); lastTick = clock.Elapsed;
        Menu();
    }
    private void Dispatch(Action action) { if (!messages.IsDisposed) messages.BeginInvoke(() => Guard(action)); }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e)
        {
            if (session.State is SessionState.Recording or SessionState.Paused)
            { Stop(); Notify("Recording stopped", e.Message); }
            else { Notify("Gaze error", e.Message); if (starting) { capture?.Dispose(); capture = null; starting = false; session.Reset(); } }
        }
    }
    private void RegisterShortcut(Settings value)
    {
        if (!Native.RegisterHotKey(messages.Handle, 1, value.Modifiers | 0x4000, (uint)value.Key))
            throw new InvalidOperationException($"{value.Shortcut} is unavailable (Windows error {Marshal.GetLastWin32Error()}). Close the conflicting application or change Gaze settings.");
    }
    private void RememberForeground()
    {
        var foreground = Native.GetForegroundWindow();
        if (Native.CaptureTargetError(foreground, (uint)Environment.ProcessId) == null)
            lastForeground = foreground;
    }
    private void Shortcut(bool fromTray = false)
    {
        if (cleaning || starting || session.State == SessionState.Saving) return;
        if (transcribe != null) { transcribe.Finish(); Menu(); return; }
        if (session.State == SessionState.Idle && settings.Mode == CaptureMode.Transcribe)
        {
            panel?.Hide(); escape.Reset();
            transcribe = new TranscribeController(settings.AutoCopy, settings.Folder, Dispatch, (exporter, exported) =>
            {
                transcribe = null; escape.Reset();
                if (exported && exporter.HasClipboardOffer) transcribeExports.Add(exporter);
                else { try { exporter.Dispose(); } catch (Exception error) { transcribeExports.Add(exporter); Notify("Temporary ZIP retained", error.Message); } }
                ResetPanel(); Menu();
            });
            Menu(); return;
        }
        if (session.State == SessionState.Idle)
        {
            var root = settings.Mode == CaptureMode.Window ? (fromTray ? lastForeground : Native.GetForegroundWindow()) : 0;
            if (settings.Mode == CaptureMode.Window)
            {
                var error = Native.CaptureTargetError(root, (uint)Environment.ProcessId);
                if (error != null) throw new InvalidOperationException(error);
            }
            if (settings.Mode == CaptureMode.CurrentMonitor)
            {
                var screen = Screen.PrimaryScreen
                    ?? throw new InvalidOperationException("No main monitor is available.");
                root = Native.MonitorHandle(screen);
            }
            panel?.Hide();
            session.Arm(settings.Mode, root); current = root;
            Notice(ArmedMessage(), 0);
        }
        else if (session.State == SessionState.Armed)
        {
            if (session.Mode == CaptureMode.Window && (!Native.IsWindow(session.Root) || Native.IsIconic(session.Root)))
                throw new InvalidOperationException("Restore the armed window before starting, or press Esc to cancel.");
            capture = new Capture();
            capture.Completed += path => Dispatch(() => Saved(path));
            capture.Failed += error => Dispatch(() => Failed(error));
            capture.StatusChanged += status => Dispatch(() =>
            {
                if (status == RecorderStatus.Recording && starting)
                { starting = false; session.Start(); lastTick = clock.Elapsed; escape.Reset(); Menu(); }
            });
            starting = true;
            capture.Start(session.Mode, session.Root, settings.FrameRate, settings.ClickHighlight, settings.Folder, windowCanvas: settings.WindowCanvas, compression: settings.Compression, splitAt29Mb: settings.SplitAt29Mb, collectionOptions: settings.CollectionOptions);
        }
        else Stop();
        Menu();
    }
    private void Stop()
    {
        if (transcribe != null) { transcribe.Finish(); return; }
        if (session.State is not (SessionState.Recording or SessionState.Paused)) return;
        session.Tick(clock.Elapsed - lastTick); lastTick = clock.Elapsed;
        session.Stop(); escape.Reset(); overlay.Hide(); noticeUntil = TimeSpan.Zero;
        capture?.Stop(); Notify("Saving…", "Finalizing the recording."); Menu();
    }
    private void Saved(IReadOnlyList<string> paths)
    {
        var path = paths[0];
        var description = paths.Count == 1 ? Path.GetFileName(path) : $"{paths.Count} recording parts saved (at most 29 MB each).";
        lastFile = path; capture?.Dispose(); capture = null; session.Reset(); overlay.Hide(); ResetPanel(); Menu();
        if (settings.AutoCopy)
        {
            try { Capture.CopyFiles(paths); Notify("Saved and copied to clipboard", description); }
            catch (Exception e) { Notify("Saved; clipboard copy failed", $"{e.Message} Right-click Gaze → Copy again."); }
        }
        else Notify("Recording saved", description);
        if (quitAfterSave) ExitThread();
    }
    private void Failed(string error)
    {
        starting = false; capture?.Dispose(); capture = null; session.Reset(); overlay.Hide(); ResetPanel(); Menu();
        Notify("Recording failed", error);
        MessageBox.Show(error, "Gaze recording failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (quitAfterSave) ExitThread();
    }
    private void Tick()
    {
        if (session.State == SessionState.Idle) RememberForeground();
        var now = clock.Elapsed; session.Tick(now - lastTick); lastTick = now;
        if (session.State is SessionState.Recording or SessionState.Paused)
        {
            if (session.Mode == CaptureMode.Window)
            {
                if (!Native.IsWindow(session.Root)) { Stop(); return; }
                if (Native.IsIconic(session.Root) && session.State == SessionState.Recording) { capture?.Pause(); session.Pause(); }
                else if (!Native.IsIconic(session.Root) && session.State == SessionState.Paused) { capture?.Resume(); session.Resume(); }
                if (session.State == SessionState.Recording)
                {
                    var followed = Native.FollowedWindow(session.Root, current);
                    capture?.Switch(followed); current = followed;
                }
            }
            if (++diskTick >= 20)
            {
                diskTick = 0;
                if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(settings.Folder))!).AvailableFreeSpace < 512L * 1024 * 1024)
                { Stop(); Notify("Stopped: disk space low", "Finalizing before available space runs out."); return; }
            }
        }
        var duration = $"{(int)session.Duration.TotalHours:00}:{session.Duration.Minutes:00}:{session.Duration.Seconds:00}";
        tray.Text = transcribe != null ? "Gaze · Transcribe" : $"Gaze · {session.State} · {duration}";
        panel?.RefreshState(transcribe != null ? SessionState.Recording : session.State, session.Duration, starting, lastFile != null);
        if (now < noticeUntil) return;
        if (session.State == SessionState.Armed)
        {
            overlay.Display(starting ? "Starting recording…" : ArmedMessage());
            return;
        }
        if (session.Mode == CaptureMode.Window && session.State is SessionState.Recording or SessionState.Paused)
            overlay.Display($"{(session.State == SessionState.Paused ? "Paused" : "● Recording")}  {duration}{(capture?.IsScaled == true ? " · scaled" : "")}");
        else overlay.Hide();
    }
    private void Notice(string text, double seconds)
    { noticeUntil = clock.Elapsed + TimeSpan.FromSeconds(seconds); overlay.Display(text); }
    private string ArmedMessage()
    {
        var title = session.Mode switch
        {
            CaptureMode.Window => Native.Title(session.Root),
            CaptureMode.CurrentMonitor => "Monitor: " + (Screen.AllScreens.FirstOrDefault(s => Native.MonitorHandle(s) == session.Root)?.DeviceName ?? "disconnected"),
            _ => "All monitors"
        };
        if (string.IsNullOrWhiteSpace(title)) title = "Untitled window";
        if (title.Length > 100) title = title[..97] + "…";
        return $"Armed: {title}\nClick Gaze or {settings.Shortcut} to record · Esc to cancel";
    }
    private nint Keyboard(int code, nint message, nint data)
    {
        if (code >= 0 && Marshal.ReadInt32(data) == 0x1B)
        {
            if (message == 0x101 || message == 0x105) escHeld = false;
            else if (message == 0x100 || message == 0x104)
            {
                bool repeat = escHeld; escHeld = true;
                if (!repeat && session.State == SessionState.Armed && !starting)
                    Dispatch(CancelArming);
                else if (settings.DoubleEscape && (transcribe != null || session.State is SessionState.Recording or SessionState.Paused))
                {
                    if (escape.Press(clock.Elapsed, repeat)) { Dispatch(Stop); return 1; }
                    if (!repeat) Dispatch(() => Notice("Press Esc again within 1 second to stop", 1));
                }
            }
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    private void Notify(string title, string body) => tray.ShowBalloonTip(5000, title, body, ToolTipIcon.Info);
    private void OpenFolder()
    {
        var folder = lastFile is null ? settings.Folder : Path.GetDirectoryName(lastFile)!;
        Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }
    private void Menu()
    {
        var state = transcribe != null ? SessionState.Recording : session.State;
        tray.Icon = icons.For(state);
        panel?.RefreshState(state, session.Duration, starting, lastFile != null);
    }
    private void ApplySettings(Settings proposed)
    {
        Native.UnregisterHotKey(messages.Handle, 1);
        try { RegisterShortcut(proposed); if (proposed.StartAtSignIn is bool startupEnabled) WindowsSignInStartup.Create().SetEnabled(startupEnabled); proposed.Save(); settings = proposed; Menu(); }
        catch { Native.UnregisterHotKey(messages.Handle, 1); RegisterShortcut(settings); throw; }
    }
    private void OpenPanel()
    {
        panel ??= new TrayPanel(() => settings, ApplySettings,
            () => Guard(() => Shortcut(fromTray: true)),
            () => Guard(CancelArming),
            path => Guard(() => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })),
            () => Guard(OpenFolder),
            path => Guard(() => { Capture.CopyFile(path); Notify("Copied", Path.GetFileName(path)); }),
            () => Guard(() => { if (transcribe != null) { if (transcribe.RequestClose()) ExitThread(); return; } quitAfterSave = true; if (starting) { quitAfterSave = false; Notify("Starting", "Wait for recording to start before quitting."); } else if (session.State is SessionState.Recording or SessionState.Paused) Stop(); else if (session.State != SessionState.Saving) ExitThread(); }),
            () => Guard(Cleanup));
        panel.RefreshState(transcribe != null ? SessionState.Recording : session.State, session.Duration, starting, lastFile != null);
        panel.OpenAt(Cursor.Position);
    }
    internal nint OpenPanelForValidation() { OpenPanel(); return panel!.Handle; }
    private void CancelArming()
    {
        session.Reset(); escape.Reset(); noticeUntil = TimeSpan.Zero; overlay.Hide();
        // Recreate the WPF surface after capture exclusion, just as after saving.
        ResetPanel(); Menu();
    }
    private void ResetPanel()
    {
        panel?.Dispose(); panel = null;
    }
    internal nint CyclePanelForValidation()
    {
        session.Arm(CaptureMode.AllMonitors, 0);
        panel!.Hide(); Menu();
        CancelArming(); OpenPanel();
        return panel!.Handle;
    }
    private void Cleanup()
    {
        if (starting || session.State != SessionState.Idle || cleaning) return;
        var directory = Path.GetFullPath(settings.Folder);
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.mp4", SearchOption.TopDirectoryOnly) : [];
        if (files.Length == 0) return;
        cleaning = true;
        try
        {
            if (MessageBox.Show(panel, $"Move all {files.Length} recordings in this folder to the Recycle Bin?\n\n{directory}", "Clean up recordings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            foreach (var file in files)
            {
                var fullPath = Path.GetFullPath(file);
                if (!string.Equals(Path.GetDirectoryName(fullPath), Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Recording is outside the save folder.");
                if (File.Exists(fullPath)) Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            if (lastFile != null && !File.Exists(lastFile)) lastFile = null;
        }
        finally { cleaning = false; panel?.RefreshRecordings(); Menu(); }
    }
    protected override void ExitThreadCore()
    {
        transcribe?.Dispose();
        foreach (var exporter in transcribeExports)
        {
            try { exporter.Dispose(); }
            catch (Exception error) { Trace.WriteLine("Transcribe temporary ZIP cleanup: " + error.Message); }
        }
        timer.Stop(); timer.Dispose(); Native.UnregisterHotKey(messages.Handle, 1);
        if (hook != 0) Native.UnhookWindowsHookEx(hook);
        capture?.Dispose(); overlay.Dispose(); tray.Visible = false; tray.Dispose(); icons.Dispose(); panel?.Dispose(); messages.Dispose(); base.ExitThreadCore();
    }
}
