using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
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
    private bool eventInputSent;
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct TestInput { [FieldOffset(0)] internal uint Type; [FieldOffset(8)] internal MouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, TestInput[] inputs, int size);
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
        capture.Completed += paths => BeginInvoke(() =>
        {
            if (args.Contains("--event-check"))
            {
                try { CheckEventMetadata(paths); }
                catch (Exception e) { Finish(false, e.ToString()); return; }
            }
            Gaze.App.Capture.CopyFiles(paths);
            bool copied = paths.All(path => Clipboard.GetFileDropList().Contains(path));
            Finish(copied, $"Output: {string.Join(", ", paths)}; clipboard: {copied}");
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
            var compression = args.Contains("--compact") ? CompressionMode.Compact : CompressionMode.Original;
            var persisted = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { WindowCanvas = canvasMode, Compression = compression }))!;
            if (persisted.Compression != compression || JsonSerializer.Deserialize<Settings>("{}")!.Compression != CompressionMode.Original)
                throw new InvalidOperationException("Compression settings did not round-trip or preserve the default.");
            if (persisted.WindowCanvas != canvasMode || JsonSerializer.Deserialize<Settings>("{}")!.WindowCanvas != WindowCanvasMode.OriginalWindow)
                throw new InvalidOperationException("Canvas settings did not round-trip or preserve the default.");
            capture.Start(args.Contains("--all-monitors") ? CaptureMode.AllMonitors : CaptureMode.Window, Handle, 5, !args.Contains("--no-click-highlight"), evidence, true, persisted.WindowCanvas, persisted.Compression, persisted.SplitAt29Mb);
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
            if (args.Contains("--event-check") && elapsed > 1 && !eventInputSent)
            {
                eventInputSent = true; Text = "Gaze synthetic fixture — Invoice 4242";
                Native.GetCursorPos(out var original);
                var point = PointToScreen(new Point(80, 100));
                Native.GetWindowThreadProcessId(Native.WindowFromPoint(point), out var clickProcess);
                if (clickProcess != (uint)Environment.ProcessId) throw new InvalidOperationException("Fixture click point is covered; no test clicks were injected.");
                SetCursorPos(point.X, point.Y);
                var inputs = Enumerable.Range(0, 10).Select(i => new TestInput { Type = 0, Mouse = new MouseInput { Flags = i % 2 == 0 ? 2u : 4u } }).ToArray();
                try { if (SendInput(10, inputs, Marshal.SizeOf<TestInput>()) != 10) throw new InvalidOperationException("Could not inject the synthetic click test."); }
                finally { SetCursorPos(original.X, original.Y); }
                Log("Five rapid synthetic clicks sent between frames; fixture title changed.");
            }
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
    private void CheckEventMetadata(IReadOnlyList<string> paths)
    {
        var allEvents = new List<JsonElement>(); var anchors = new List<JsonElement>(); int chapterCount = 0;
        foreach (var path in paths)
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GAZE_TEST_PYTHON") ?? "python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "Read-Timeline.py"), path, "--probe", Path.Combine(AppContext.BaseDirectory, "ffprobe.exe") }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) { process.Kill(); throw new InvalidOperationException("Timeline reader timed out."); }
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult());
            string json = output.GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(evidence, Path.GetFileNameWithoutExtension(path) + ".timeline.json"), json);
            using var data = JsonDocument.Parse(json);
            allEvents.AddRange(data.RootElement.GetProperty("events").EnumerateArray().Select(e => e.Clone()));
            anchors.AddRange(data.RootElement.GetProperty("clockAnchors").EnumerateArray().Select(e => e.Clone()));
            chapterCount += data.RootElement.GetProperty("chapters").GetArrayLength();
        }
        var clicks = allEvents.Where(e => e.GetProperty("kind").GetString() == "click").ToArray();
        if (clicks.Length < 5 || clicks.GroupBy(e => e.GetProperty("frame").GetInt32()).Max(g => g.Count()) < 4)
            throw new InvalidOperationException("Rapid clicks between video frames were not preserved.");
        if (!allEvents.Any(e => e.GetProperty("kind").GetString() == "window" && e.TryGetProperty("title", out var title) && title.GetString()!.Contains("Invoice 4242")) || chapterCount < 2)
            throw new InvalidOperationException("Actual window title changes or chapters are missing.");
        if (!args.Contains("--all-monitors"))
        {
            var external = allEvents.Where(e => e.TryGetProperty("external", out var flag) && flag.GetBoolean()).ToArray();
            if (external.Length == 0 || external.Any(e => e.TryGetProperty("title", out _) || e.TryGetProperty("process", out _) || e.TryGetProperty("windowId", out _)))
                throw new InvalidOperationException("Foreign foreground context was not anonymized.");
            if (allEvents.Any(e => e.TryGetProperty("title", out var title) && title.GetString() == "Unrelated covering window"))
                throw new InvalidOperationException("The unrelated covering window's title leaked.");
            if (!allEvents.Any(e => e.TryGetProperty("title", out var title) && title.GetString() == "Owned fixture dialog"))
                throw new InvalidOperationException("Captured owned dialog title was lost.");
            Log("PASS: external foreground events have no identity; captured owned dialog retains its title.");
        }
        if (!anchors.Any(a => a.GetProperty("reason").GetString() == "resume") || !allEvents.Any(e => e.GetProperty("kind").GetString() == "pause"))
            throw new InvalidOperationException("Pause/resume clock mapping is missing.");
        Log($"PASS: {clicks.Length} independent clicks, {chapterCount} chapters, resume clock anchor; event track extracted from actual recording.");
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
