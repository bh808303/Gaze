using System.Diagnostics;
using System.Text.Json;
using Gaze.Core;

namespace Gaze.App;

// Records only synthetic fixture content unless --all-monitors is explicitly selected.
internal sealed class CaptureFixture : Form
{
    private readonly string evidence;
    private readonly string[] args;
    private readonly Stopwatch clock = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private Capture? capture;
    private Form? dialog, cover;
    private readonly Overlay overlay = new();
    private readonly List<string> events = [];
    private bool opened, closed, paused, resumed, stopping;
    private bool checkedDialogScale;
    private double recordingStart;
    private nint current;
    internal CaptureFixture(string[] args)
    {
        this.args = args;
        var i = Array.IndexOf(args, "--evidence");
        evidence = i >= 0 ? Path.GetFullPath(args[i + 1]) : Path.GetFullPath("docs/factory/runs/prototype-001/runtime");
        Directory.CreateDirectory(evidence);
        Text = "Gaze synthetic capture fixture"; StartPosition = FormStartPosition.Manual;
        Location = new Point(80, 80); ClientSize = new Size(900, 560);
        BackColor = Color.FromArgb(240, 245, 250);
        var content = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16), ForeColor = Color.FromArgb(20, 45, 70), Text = "GAZE WINDOW CAPTURE TEST\n\nReadable text: abcdefghijklmnopqrstuvwxyz 0123456789\n\nInvoice 12345 · Status: Pending\n\nThis text must remain visible while covered by another window.\n\nDialog capture must return here after closing.\n\nNative resolution · 5 FPS · no audio", Padding = new Padding(24) };
        Controls.Add(content);
        Shown += async (_, _) =>
        {
            Native.ShowWindow(Handle, 5);
            Show(); Activate(); Refresh();
            await Task.Delay(600);
            Begin();
        };
        timer.Tick += (_, _) => Step();
    }
    private void Log(string message) { events.Add($"{clock.Elapsed.TotalSeconds:F2}s {message}"); File.WriteAllLines(Path.Combine(evidence, "events.txt"), events); }
    private void Begin()
    {
        clock.Start(); current = Handle;
        Log($"Fixture HWND {Handle}, visible {Native.IsWindowVisible(Handle)}, minimized {Native.IsIconic(Handle)}");
        Log(Native.DesktopInfo());
        Log(Native.CaptureInfo(Handle));
        using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(evidence, "fixture-render.png")); }
        capture = new Capture();
        capture.StatusChanged += status => BeginInvoke(() =>
        {
            Log($"Recorder {status}");
            if (status == RecorderStatus.Recording && !timer.Enabled) { recordingStart = clock.Elapsed.TotalSeconds; timer.Start(); }
        });
        capture.Failed += error => BeginInvoke(() => Finish(false, error));
        capture.Completed += path => BeginInvoke(() =>
        {
            Gaze.App.Capture.CopyFile(path);
            bool copied = Clipboard.GetFileDropList().Contains(path);
            Finish(copied, $"Output: {path}; clipboard: {copied}");
        });
        try
        {
            if (Native.CaptureTargetError(0, (uint)Environment.ProcessId) == null ||
                Native.CaptureTargetError(Handle, (uint)Environment.ProcessId) == null ||
                Native.CaptureTargetError(Handle, 0) != null)
                throw new InvalidOperationException("Capture target validation failed for absent, own, or application windows.");
            int rejectedShellWindows = 0;
            Native.EnumWindows((hwnd, _) =>
            {
                if (Native.CaptureTargetError(hwnd, 0)?.StartsWith("The taskbar and desktop") == true) rejectedShellWindows++;
                return true;
            }, 0);
            if (rejectedShellWindows == 0) throw new InvalidOperationException("No shell surfaces found to verify taskbar rejection.");
            Log($"Target validation passed; {rejectedShellWindows} shell surfaces rejected.");
            var canvasMode = args.Contains("--monitor-canvas") ? WindowCanvasMode.Monitor : WindowCanvasMode.OriginalWindow;
            var persisted = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { WindowCanvas = canvasMode }))!;
            if (persisted.WindowCanvas != canvasMode || JsonSerializer.Deserialize<Settings>("{}")!.WindowCanvas != WindowCanvasMode.OriginalWindow)
                throw new InvalidOperationException("Canvas settings did not round-trip or preserve the default.");
            capture.Start(args.Contains("--all-monitors") ? CaptureMode.AllMonitors : CaptureMode.Window, Handle, 5, true, evidence, true, persisted.WindowCanvas);
            var expected = canvasMode == WindowCanvasMode.Monitor ? Screen.FromHandle(Handle).Bounds.Size : Size;
            if (Math.Abs(capture.Canvas.Width - expected.Width) > 24 || Math.Abs(capture.Canvas.Height - expected.Height) > 24)
                throw new InvalidOperationException("Canvas does not match the selected policy.");
            if (!args.Contains("--no-overlay")) { overlay.Location = new Point(Left + 100, Top + 100); overlay.Display("GAZE OVERLAY MUST NOT APPEAR"); }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Step()
    {
        try
        {
            double elapsed = clock.Elapsed.TotalSeconds - recordingStart;
            if (elapsed > 2 && cover == null)
            {
                cover = new Form { Text = "Unrelated covering window", StartPosition = FormStartPosition.Manual, Location = Location, Size = Size, BackColor = Color.Magenta };
                cover.Controls.Add(new Label { Text = "UNRELATED WINDOW MUST NOT APPEAR IN WINDOW CAPTURE", Dock = DockStyle.Fill }); cover.Show(); Log("Root covered by unrelated window");
            }
            if (elapsed > 4 && !opened)
            {
                opened = true;
                dialog = new Form { Text = "Owned fixture dialog", StartPosition = FormStartPosition.Manual, Location = new Point(Left + 60, Top + 60), ClientSize = args.Contains("--large-dialog") ? new Size(1200, 800) : new Size(500, 300), BackColor = Color.FromArgb(215, 245, 220) };
                dialog.Controls.Add(new Label { Text = "OWNED DIALOG\n\nExpected captured text: Dialog 67890\n\nWindow switches temporarily.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16), Padding = new Padding(24) });
                dialog.Show(this); Log("Owned dialog opened");
            }
            if (elapsed > 7 && !closed) { closed = true; dialog?.Close(); cover?.Close(); Log("Dialog and covering window closed"); }
            if (elapsed > 6 && !checkedDialogScale && args.Contains("--large-dialog"))
            {
                checkedDialogScale = true;
                var expectedScaled = args.Contains("--monitor-canvas")
                    ? dialog!.Width > capture!.Canvas.Width || dialog.Height > capture.Canvas.Height
                    : true;
                if (capture!.IsScaled != expectedScaled) throw new InvalidOperationException("Larger dialog scaling does not match the fixed canvas.");
                Log($"Large dialog scaling verified: {capture.IsScaled}");
            }
            if (!args.Contains("--all-monitors"))
            {
                var next = Native.FollowedWindow(Handle, current);
                if (next != current) { capture!.Switch(next); current = next; Log($"Switched to {Native.Title(next)}"); }
            }
            if (elapsed > 8 && !paused) { paused = true; capture!.Pause(); Log("Paused"); }
            if (elapsed > 9.5 && !resumed) { resumed = true; capture!.Resume(); Log("Resumed"); }
            if (elapsed > 12 && !stopping)
            {
                stopping = true;
                Log("Stopping"); capture!.Stop();
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Finish(bool success, string detail)
    {
        timer.Stop(); overlay.Close(); dialog?.Close(); cover?.Close();
        Log(detail);
        File.WriteAllText(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new { success, detail, canvas = capture?.Canvas, events }, new JsonSerializerOptions { WriteIndented = true }));
        capture?.Dispose(); Environment.ExitCode = success ? 0 : 1; Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); overlay.Dispose(); capture?.Dispose(); }
        base.Dispose(disposing);
    }
}
