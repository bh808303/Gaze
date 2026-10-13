using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Gaze.Core;

namespace Gaze.App;

internal enum RecorderStatus { Idle, Recording, Paused, Finishing }

internal sealed class Capture : IDisposable
{
    private readonly object gate = new();
    private readonly ClickQueue pendingClicks = new();
    private readonly MouseQueue pendingMouse = new();
    private readonly PointerSampler pointerSampler = new();
    private bool metadata;
    private readonly Dictionary<string, (Rectangle Source, Rectangle Destination)> mappings = [];
    private readonly KeyboardQueue pendingKeys = new();
    private readonly HashSet<int> keysDown = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> pendingWarnings = new();
    private GraphicsSource? window;
    private nint target;
    private nint recordingRoot;
    private readonly List<(string Device, Rectangle Bounds, GraphicsSource Source)> monitors = [];
    private CancellationTokenSource? cancel;
    private Task? worker;
    private Process? encoder;
    private volatile bool paused, stopping, disposed;
    private int disposeOnce;
    private string folder = "";
    private bool diagnostics, clicks;
    private int fps;
    private CompressionMode compression;
    private bool splitAt29Mb;
    private CaptureMode mode;
    private long clickUntil;
    private Point clickPoint;
    private Rectangle clickSource, clickDestination;
    private RecordingTimeline? timeline;
    private InputEvents? input;
    private Rectangle desktopBounds;
    private IReadOnlyList<Rectangle> captureAreas = [];
    private CapturedFrame? previous;
    internal event Action<IReadOnlyList<string>>? Completed;
    internal event Action<string>? Failed;
    internal event Action<RecorderStatus>? StatusChanged;
    internal Size Canvas { get; private set; }
    internal bool IsScaled { get; private set; }

    internal static string EncoderPath()
    {
        var configured = Environment.GetEnvironmentVariable("GAZE_FFMPEG");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(bundled)) return bundled;
        // Development fallback follows the executable location, never the launcher's working directory.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var local = Path.Combine(directory.FullName, ".tools", "imageio_ffmpeg", "binaries", "ffmpeg-win-x86_64-v7.1.exe");
            if (File.Exists(local)) return local;
        }
        throw new FileNotFoundException("FFmpeg is required. Place ffmpeg.exe beside Gaze, or set GAZE_FFMPEG to its path.");
    }
    internal void Start(CaptureMode mode, nint root, int fps, bool clicks, string folder, bool diagnostics = false, WindowCanvasMode windowCanvas = WindowCanvasMode.OriginalWindow, CompressionMode compression = CompressionMode.Original, bool splitAt29Mb = false, bool keyboardTimeline = false, bool recordTimelineMetadata = false, TimelineCollectionOptions? collectionOptions = null)
    {
        if (!Enum.IsDefined(compression)) throw new ArgumentOutOfRangeException(nameof(compression));
        this.compression = compression;
        this.splitAt29Mb = splitAt29Mb;
        recordingRoot = root;
        _ = EncoderPath(); this.mode = mode; this.fps = fps; this.folder = folder; this.diagnostics = diagnostics; this.clicks = clicks;
        Directory.CreateDirectory(folder);
        var collection = collectionOptions ?? new() { RecordTimelineMetadata = recordTimelineMetadata, KeyboardTimeline = keyboardTimeline };
        metadata = collection.RecordTimelineMetadata;
        keyboardTimeline = collection.KeyboardTimeline;
        timeline = new RecordingTimeline(fps, collection);
        timeline.Status("mouse", "initializing");
        timeline.Status("window", "initializing");
        timeline.Status("keyboard", keyboardTimeline ? "initializing" : "disabled");
        if (mode == CaptureMode.Window)
        {
            target = root; window = new GraphicsSource(root, false, fps);
            Canvas = Even(windowCanvas switch
            {
                WindowCanvasMode.OriginalWindow => window.NativeSize,
                WindowCanvasMode.Monitor => Screen.FromHandle(root).Bounds.Size,
                _ => throw new ArgumentOutOfRangeException(nameof(windowCanvas))
            });
            ValidateCanvas(Canvas);
        }
        else
        {
            var screens = mode == CaptureMode.CurrentMonitor
                ? Screen.AllScreens.Where(s => Native.MonitorHandle(s) == root).ToArray()
                : Screen.AllScreens;
            if (screens.Length == 0) throw new InvalidOperationException("The armed monitor is no longer connected. Cancel and arm again.");
            var bounds = screens.Select(s => s.Bounds).Aggregate(Rectangle.Union); Canvas = Even(bounds.Size); ValidateCanvas(Canvas);
            desktopBounds = bounds;
            captureAreas = screens.Select(screen => screen.Bounds).ToArray();
            foreach (var screen in screens)
            {
                var monitor = Native.MonitorFromPoint(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2);
                monitors.Add((screen.DeviceName, new Rectangle(screen.Bounds.X - bounds.X, screen.Bounds.Y - bounds.Y, screen.Bounds.Width, screen.Bounds.Height), new GraphicsSource(monitor, true, fps)));
            }
        }
        try { input = new InputEvents(QueueClick, timeline.Window, Warn, () => mode == CaptureMode.Window ? target : 0, mode, root, captureAreas, keyboardTimeline ? QueueKey : null, metadata ? QueueMouse : null);
            timeline.Status("mouse", "installed"); timeline.Status("window", "installed");
            timeline.Status("keyboard", keyboardTimeline ? "installed" : "disabled"); }
        catch (Exception e) { timeline.Status("mouse", "hook-installation-failed"); timeline.Status("window", "hook-installation-failed"); if (keyboardTimeline) timeline.Status("keyboard", "hook-installation-failed"); Warn($"Input event recording unavailable: {e.Message}"); }
        cancel = new(); worker = Task.Run(() => Encode(cancel.Token));
    }
    internal static void ValidateCanvas(Size canvas)
    {
        if (canvas.Width < 2 || canvas.Height < 2 || canvas.Width > 8192 || canvas.Height > 8192 || (long)canvas.Width * canvas.Height > 40_000_000)
            throw new InvalidOperationException($"Native canvas {canvas.Width} × {canvas.Height} exceeds Gaze's encoder limit. Select a smaller window; no resolution was reduced.");
    }
    private async Task Encode(CancellationToken token)
    {
        var partial = Path.Combine(folder, $"Gaze_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid():N}.mp4.partial");
        try
        {
            var timeout = Stopwatch.StartNew();
            while (!Ready())
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(8)) throw new InvalidOperationException("Windows did not supply a capture frame. Restore the target or check capture permissions.");
                await Task.Delay(25, token);
            }
            var start = new ProcessStartInfo(EncoderPath()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{Canvas.Width}x{Canvas.Height}", "-framerate", fps.ToString(), "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", compression == CompressionMode.Compact ? "medium" : "veryfast", "-crf", compression == CompressionMode.Compact ? "24" : "18", "-pix_fmt", "yuv420p", "-g", (fps * (compression == CompressionMode.Compact ? 10 : 2)).ToString(), "-movflags", "+frag_keyframe+empty_moov+default_base_moof", "-f", "mp4", partial }) start.ArgumentList.Add(arg);
            encoder = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg.");
            var recordingStart = DateTime.Now;
            var stderr = encoder.StandardError.ReadToEndAsync(); StatusChanged?.Invoke(RecorderStatus.Recording);
            using var canvas = new Bitmap(Canvas.Width, Canvas.Height, PixelFormat.Format32bppArgb);
            var bytes = new byte[checked(Canvas.Width * Canvas.Height * 4)]; int frameNumber = 0;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / fps));
            try
            {
                do
                {
                    FlushWarnings();
                    lock (gate)
                    {
                        pendingClicks.Drain(RecordClick); DrainMouse();
                        pendingKeys.Drain(RecordKey);
                        if (paused || stopping) continue;
                        timeline!.Frame(DateTimeOffset.Now);
                        Render(canvas);
                    }
                    var data = canvas.LockBits(new Rectangle(Point.Empty, Canvas), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try { Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); } finally { canvas.UnlockBits(data); }
                    // Complete a whole raw frame before stopping; cancellation midway corrupts the pipe's final frame.
                    await encoder.StandardInput.BaseStream.WriteAsync(bytes);
                    if (diagnostics && frameNumber % fps == 0)
                    { Directory.CreateDirectory(Path.Combine(folder, "snapshots")); canvas.Save(Path.Combine(folder, "snapshots", $"frame-{frameNumber / fps:000}.png")); }
                    frameNumber++;
                } while (await timer.WaitForNextTickAsync(token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            var recordingEnd = DateTime.Now;
            timeline!.Stop();
            StatusChanged?.Invoke(RecorderStatus.Finishing); encoder.StandardInput.Close();
            using var finishTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await encoder.WaitForExitAsync(finishTimeout.Token);
            var error = await stderr;
            if (encoder.ExitCode != 0) throw new InvalidOperationException($"FFmpeg failed: {error}");
            if (frameNumber == 0) throw new InvalidOperationException("No frames were recorded.");
            var finalPath = Path.Combine(folder, RecordingName.Create(recordingStart, recordingEnd, TimeSpan.FromSeconds((double)frameNumber / fps)));
            var basePath = finalPath;
            for (int suffix = 2; File.Exists(finalPath); suffix++) finalPath = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(basePath)}-{suffix}.mp4");
            File.Move(partial, finalPath); partial = finalPath;
            var events = timeline.Snapshot(frameNumber);
            IReadOnlyList<string> files;
            if (splitAt29Mb) files = await RecordingSplitter.CreateAsync(finalPath, frameNumber, EncoderPath(), fps, compression, events, Warn);
            else
            {
                await RecordingMetadata.TryEmbedAsync(finalPath, events.Slice(0, frameNumber), EncoderPath(), Warn);
                files = new[] { finalPath };
            }
            StatusChanged?.Invoke(RecorderStatus.Idle); Completed?.Invoke(files);
        }
        catch (Exception e)
        {
            timeline?.Stop();
            if (encoder is { HasExited: false }) encoder.Kill(); StatusChanged?.Invoke(RecorderStatus.Idle);
            if (!disposed) Failed?.Invoke($"{e.Message}\nWorking file retained at {partial}");
        }
        finally { FlushWarnings(); }
    }
    private bool Ready() { lock (gate) return mode == CaptureMode.Window ? window?.Latest != null : monitors.All(m => m.Source.Latest != null); }
    private void Render(Bitmap canvas)
    {
        using var graphics = Graphics.FromImage(canvas); graphics.Clear(Color.Black);
        lock (gate)
        {
            if (mode == CaptureMode.Window)
            {
                if (window?.Error != null) throw new InvalidOperationException(window.Error);
                var frame = window?.Latest ?? previous;
                if (frame != null)
                {
                    previous = frame; double scale = Math.Min(1, Math.Min((double)Canvas.Width / frame.Width, (double)Canvas.Height / frame.Height)); IsScaled = scale < 1;
                    var size = new Size((int)(frame.Width * scale), (int)(frame.Height * scale));
                    var destination = new Rectangle((Canvas.Width - size.Width) / 2, (Canvas.Height - size.Height) / 2, size.Width, size.Height);
                    Draw(graphics, frame, destination);
                    if (Native.IsWindow(target))
                    {
                        clickSource = Native.Bounds(target); clickDestination = destination;
                        RecordGeometry(clickSource, destination, "captured");
                        if (clicks) Highlight(graphics, clickSource, clickDestination);
                    }
                }
            }
            else
            {
                var connected = Screen.AllScreens.Select(s => s.DeviceName).ToHashSet();
                foreach (var monitor in monitors)
                {
                    if (!connected.Contains(monitor.Device) || monitor.Source.Closed)
                    {
                        mappings.Remove(monitor.Device);
                        RecordGeometry(Rectangle.Empty, Rectangle.Empty, monitor.Device);
                        mappings.Remove(monitor.Device);
                        continue;
                    }
                    if (monitor.Source.Error != null) throw new InvalidOperationException(monitor.Source.Error);
                    var frame = monitor.Source.Latest; if (frame != null) { Draw(graphics, frame, monitor.Bounds);
                    var actual = Screen.AllScreens.First(s => s.DeviceName == monitor.Device).Bounds;
                    RecordGeometry(new Rectangle(actual.Location, new Size(frame.Width,frame.Height)), monitor.Bounds, monitor.Device); }
                }
                if (clicks) Highlight(graphics, desktopBounds, new Rectangle(Point.Empty, desktopBounds.Size));
            }
        }
    }
    private static void Draw(Graphics graphics, CapturedFrame frame, Rectangle destination)
    {
        var pin = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
        try
        {
            using var source = new Bitmap(frame.Width, frame.Height, frame.Width * 4, PixelFormat.Format32bppArgb, pin.AddrOfPinnedObject());
            graphics.DrawImage(source, destination, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel);
        }
        finally { pin.Free(); }
    }
    private void Highlight(Graphics graphics, Rectangle source, Rectangle destination)
    {
        var cursor = clickPoint;
        if (Environment.TickCount64 > clickUntil || !source.Contains(cursor)) return;
        float x = destination.X + (cursor.X - source.X) * destination.Width / (float)source.Width, y = destination.Y + (cursor.Y - source.Y) * destination.Height / (float)source.Height;
        using var pen = new Pen(Color.Gold, 3); graphics.DrawEllipse(pen, x - 14, y - 14, 28, 28);
    }
    internal void Switch(nint hwnd)
    {
        lock (gate)
        {
            if (target == hwnd || stopping) return;
            var replacement = new GraphicsSource(hwnd, false, fps); var old = window; window = replacement; target = hwnd; old?.Dispose();
            try { input?.CapturedWindowChanged(hwnd); } catch (Exception e) { Warn($"Captured-window event could not be recorded: {e.Message}"); }
        }
    }
    private void QueueClick(string button, Point point, DateTimeOffset time)
    {
        if (!paused && !stopping && !disposed) pendingClicks.Enqueue(button, point, time);
    }
    private void QueueMouse(RawMouse value)
    {
        if (!paused && !stopping && !disposed) pendingMouse.Enqueue(value);
    }
    private void DrainMouse()
    {
        pendingMouse.Drain(RecordMouse);
        if (pendingMouse.Lost > 0) timeline?.Status("mouse", "loss", pendingMouse.Lost);
    }
    private void RecordMouse(RawMouse value)
    {
        int x = 0, y = 0;
        bool included;
        if (mode != CaptureMode.Window)
            included = TimelineBoundary.TryMapMonitorPoint(value.Point, captureAreas, mappings.Values, out x, out y);
        else
        {
            var source = clickSource;
            var destination = clickDestination;
            included = source.Width > 0 && source.Height > 0 && source.Contains(value.Point);
            if (included)
            {
                var hit = Native.GetAncestor(Native.WindowFromPoint(value.Point), 2);
                included = hit != 0 && Native.RecordedClickWindow(hit, recordingRoot, target);
            }
            if (included)
            {
                x = destination.X + (int)((value.Point.X - source.X) * (long)destination.Width / source.Width);
                y = destination.Y + (int)((value.Point.Y - source.Y) * (long)destination.Height / source.Height);
            }
        }
        foreach (var item in pointerSampler.Sample(included, value.Point.X, value.Point.Y, x, y, value.Time, value.Milliseconds, value.Held, value.Kind == "pointer"))
            timeline?.Metadata(item);
        if (!included || value.Kind == "pointer") return;
        if (value.Kind == "click")
        {
            timeline?.Click(value.Button!, value.Point.X, value.Point.Y, x, y, value.Time);
            UpdateClickHighlight(value.Button!, value.Point, value.Time);
        }
        else timeline?.Metadata(new(0,0,value.Time,value.Kind,Button:value.Button,ScreenX:value.Point.X,ScreenY:value.Point.Y,X:x,Y:y,
            Delta:value.Kind == "wheel" ? value.Delta : null, Axis:value.Axis));
    }
    private void RecordGeometry(Rectangle source, Rectangle destination, string scope)
    {
        mappings[scope] = (source, destination);
        timeline?.Geometry(new(source.X,source.Y,source.Width,source.Height,destination.X,destination.Y,destination.Width,destination.Height,Canvas.Width,Canvas.Height,scope), DateTimeOffset.Now);
    }
    private void QueueKey(RawKey key)
    {
        if (!paused && !stopping && !disposed) pendingKeys.Enqueue(key);
    }
    private void RecordKey(RawKey key)
    {
        var root = Native.GetAncestor(key.Foreground, 2);
        if (root == 0 || !Native.IsWindow(root)) return;
        bool related = mode == CaptureMode.Window && (root == target || Native.OwnedBy(root, recordingRoot));
        if (!TimelineBoundary.IncludesWindow(mode, related, mode == CaptureMode.Window ? Rectangle.Empty : Native.Bounds(root), captureAreas))
        { keysDown.Clear(); return; }
        bool repeat = key.Down && !keysDown.Add(key.VirtualKey);
        if (!key.Down) keysDown.Remove(key.VirtualKey);
        timeline?.Keyboard(key.VirtualKey, key.ScanCode, ((Keys)key.VirtualKey).ToString(), key.Down, repeat, (key.Flags & 1) != 0, (key.Flags & 16) != 0, key.Time);
    }
    private void RecordClick(string button, Point point, DateTimeOffset time)
    {
        lock (gate)
        {
            var source = mode == CaptureMode.Window ? clickSource : desktopBounds;
            if (mode != CaptureMode.Window && !TimelineBoundary.IncludesPoint(point, captureAreas)) return;
            var destination = mode == CaptureMode.Window ? clickDestination : new Rectangle(Point.Empty, desktopBounds.Size);
            if (source.Width <= 0 || source.Height <= 0 || !source.Contains(point)) return;
            if (mode == CaptureMode.Window)
            {
                // Hit-test on the consumer, never inside the low-level mouse hook.
                var hit = Native.GetAncestor(Native.WindowFromPoint(point), 2);
                if (hit == 0 || !Native.RecordedClickWindow(hit, recordingRoot, target)) return;
            }
            int x = destination.X + (int)((point.X - source.X) * (long)destination.Width / source.Width);
            int y = destination.Y + (int)((point.Y - source.Y) * (long)destination.Height / source.Height);
            timeline?.Click(button, point.X, point.Y, x, y, time);
            UpdateClickHighlight(button, point, time);
        }
    }
    private void UpdateClickHighlight(string button, Point point, DateTimeOffset time)
    {
        if (button == "left") { clickPoint = point; clickUntil = Environment.TickCount64 + 400 - Math.Max(0, (long)(DateTimeOffset.Now - time).TotalMilliseconds); }
    }
    private void Warn(string message)
    {
        if (message.Contains("input events could not be recorded", StringComparison.OrdinalIgnoreCase) || message.Contains("Captured-window event could not be recorded", StringComparison.Ordinal))
            timeline?.Status("input", "processing-error");
        pendingWarnings.Enqueue($"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
    }
    private void FlushWarnings()
    {
        while (pendingWarnings.TryDequeue(out var message))
        {
            try { File.AppendAllText(Path.Combine(folder, "Gaze-warnings.log"), message); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    internal void Pause() { lock (gate) { paused = true; pendingClicks.Drain(RecordClick); DrainMouse(); pendingKeys.Drain(RecordKey); keysDown.Clear(); pendingMouse.ResetSampling(); pointerSampler.Reset(); timeline?.Pause(DateTimeOffset.Now); } StatusChanged?.Invoke(RecorderStatus.Paused); }
    internal void Resume() { lock (gate) { timeline?.Resume(DateTimeOffset.Now); paused = false; } StatusChanged?.Invoke(RecorderStatus.Recording); }
    internal void Stop() { if (disposed) return; lock (gate) { stopping = true; pendingClicks.Drain(RecordClick); DrainMouse(); pendingKeys.Drain(RecordKey); timeline?.Stop(); } cancel?.Cancel(); }
    internal static void CopyFile(string path)
        => CopyFiles(new[] { path });
    internal static void CopyFiles(IReadOnlyList<string> paths)
    {
        var files = new StringCollection();
        foreach (var path in paths)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The saved recording no longer exists.", path);
            files.Add(path);
        }
        Clipboard.SetFileDropList(files);
    }
    private static Size Even(Size size) => new((size.Width + 1) & ~1, (size.Height + 1) & ~1);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeOnce, 1) != 0) return;
        disposed = true; cancel?.Cancel();
        timeline?.Stop(); input?.Dispose(); input = null;
        lock (gate) { window?.Dispose(); window = null; foreach (var m in monitors) m.Source.Dispose(); monitors.Clear(); }
        void Release() { encoder?.Dispose(); cancel?.Dispose(); }
        if (worker == null || worker.IsCompleted) Release();
        else _ = worker.ContinueWith(_ => Release(), TaskScheduler.Default);
    }
}
