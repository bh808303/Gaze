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
    private GraphicsSource? window;
    private nint target;
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
    internal void Start(CaptureMode mode, nint root, int fps, bool clicks, string folder, bool diagnostics = false, WindowCanvasMode windowCanvas = WindowCanvasMode.OriginalWindow, CompressionMode compression = CompressionMode.Original, bool splitAt29Mb = false)
    {
        if (!Enum.IsDefined(compression)) throw new ArgumentOutOfRangeException(nameof(compression));
        this.compression = compression;
        this.splitAt29Mb = splitAt29Mb;
        _ = EncoderPath(); this.mode = mode; this.fps = fps; this.folder = folder; this.diagnostics = diagnostics; this.clicks = clicks;
        Directory.CreateDirectory(folder);
        timeline = new RecordingTimeline(fps);
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
            foreach (var screen in screens)
            {
                var monitor = Native.MonitorFromPoint(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2);
                monitors.Add((screen.DeviceName, new Rectangle(screen.Bounds.X - bounds.X, screen.Bounds.Y - bounds.Y, screen.Bounds.Width, screen.Bounds.Height), new GraphicsSource(monitor, true, fps)));
            }
        }
        try { input = new InputEvents(RecordClick, timeline.Window, Warn, () => mode == CaptureMode.Window ? target : 0); }
        catch (Exception e) { Warn($"Input event recording unavailable: {e.Message}"); }
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
                    lock (gate)
                    {
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
                        if (clicks) Highlight(graphics, clickSource, clickDestination);
                    }
                }
            }
            else
            {
                var connected = Screen.AllScreens.Select(s => s.DeviceName).ToHashSet();
                foreach (var monitor in monitors)
                {
                    if (!connected.Contains(monitor.Device) || monitor.Source.Closed) continue;
                    if (monitor.Source.Error != null) throw new InvalidOperationException(monitor.Source.Error);
                    var frame = monitor.Source.Latest; if (frame != null) Draw(graphics, frame, monitor.Bounds);
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
    private void RecordClick(string button, Point point, DateTimeOffset time)
    {
        lock (gate)
        {
            if (paused || stopping || disposed) return;
            var source = mode == CaptureMode.Window ? clickSource : desktopBounds;
            var destination = mode == CaptureMode.Window ? clickDestination : new Rectangle(Point.Empty, desktopBounds.Size);
            if (source.Width <= 0 || source.Height <= 0 || !source.Contains(point)) return;
            int x = destination.X + (int)((point.X - source.X) * (long)destination.Width / source.Width);
            int y = destination.Y + (int)((point.Y - source.Y) * (long)destination.Height / source.Height);
            timeline?.Click(button, point.X, point.Y, x, y, time);
            if (button == "left") { clickPoint = point; clickUntil = Environment.TickCount64 + 400; }
        }
    }
    private void Warn(string message)
    {
        try { lock (gate) File.AppendAllText(Path.Combine(folder, "Gaze-warnings.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    internal void Pause() { lock (gate) { paused = true; timeline?.Pause(DateTimeOffset.Now); } StatusChanged?.Invoke(RecorderStatus.Paused); }
    internal void Resume() { lock (gate) { timeline?.Resume(DateTimeOffset.Now); paused = false; } StatusChanged?.Invoke(RecorderStatus.Recording); }
    internal void Stop() { if (disposed) return; lock (gate) { stopping = true; timeline?.Stop(); } cancel?.Cancel(); }
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
