using System.Reflection;
using System.Drawing.Imaging;

namespace Gaze.App;

internal static class TranscribeCaptureProbe
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetThreadDpiAwarenessContext();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out Point point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
    private static Point PhysicalPointer() => GetPhysicalCursorPos(out var point) ? point : throw new InvalidOperationException("Cannot read physical pointer position.");
    private static void MovePointer(Point point)
    {
        if (!SetPhysicalCursorPos(point.X, point.Y) || PhysicalPointer() != point)
            throw new InvalidOperationException($"Cannot place physical pointer at {point}.");
    }
    internal static void Run(string evidence)
    {
        Directory.CreateDirectory(evidence);
        using var target = new Form { Text = "Transcribe native fixture", BackColor = Color.Red, StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(Screen.PrimaryScreen!.WorkingArea.Location + new Size(80, 80), new Size(360, 260)) };
        target.Shown += async (_, _) =>
        {
            var results = new List<string> { $"OS: {Environment.OSVersion}; runtime: {Environment.Version}; monitors: {Screen.AllScreens.Length}; DPI: {target.DeviceDpi}" };
            try
            {
                // PointToScreen and Screen.Bounds must share WGC's physical pixel space.
                // The standalone executable must declare its own DPI mode/manifest.
                if (!AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), (nint)(-4)))
                    throw new InvalidOperationException("Probe requires PerMonitorV2 before creating fixture controls.");
                results.Add("DPI mapping: PerMonitorV2 verified; screen/client mapping and WGC crop use physical pixels");
                bool failed = false;
                async Task RunCheck(string name, Func<Task> check)
                {
                    try { await check(); }
                    catch (Exception error) { failed = true; results.Add($"FAIL: {name}: {error}"); }
                }
                var type = Assembly.Load("Gaze.App").GetType("Gaze.App.TranscribeScreenshotSource") ?? throw new InvalidOperationException("Missing screenshot source: no cursor-free PNG capture behavior exists.");
                async Task<byte[]> Capture(string method, params object[] arguments) => await (Task<byte[]>)type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments)!;
                void Check(byte[] png, Color expected, string name)
                {
                    using var stream = new MemoryStream(png); using var image = new Bitmap(stream);
                    var pixel = image.GetPixel(image.Width / 2, image.Height / 2);
                    if (pixel.ToArgb() != expected.ToArgb()) throw new InvalidOperationException($"{name}: expected {expected}, got {pixel}");
                    File.WriteAllBytes(Path.Combine(evidence, name + ".png"), png); results.Add("PASS: " + name);
                }
                ShowWindow(target.Handle, 9); target.Refresh();
                await Task.Delay(500);
                var retained = await Capture("CaptureWindowAsync", target.Handle);
                Check(retained, Color.Red, "full-window");
                using var cover = new Form { BackColor = Color.Blue, StartPosition = FormStartPosition.Manual, Bounds = target.Bounds, TopMost = true };
                cover.Show(); target.BackColor = Color.Lime; target.Refresh(); await Task.Delay(500);
                Check(await Capture("CaptureWindowAsync", target.Handle), Color.Lime, "occluded-fresh-window");
                var region = new Rectangle(target.PointToScreen(new Point(40, 60)), new Size(100, 80));
                Check(await Capture("CaptureRectangleAsync", region, Screen.FromHandle(target.Handle).Bounds), Color.Blue, "visible-rectangle");
                var monitor = Screen.FromHandle(target.Handle).Bounds;
                using (var stream = new MemoryStream(await Capture("CaptureRectangleAsync", new Rectangle(monitor.Right - 10, monitor.Bottom - 10, 50, 50), monitor)))
                using (var image = new Bitmap(stream))
                    if (image.Width != 10 || image.Height != 10) throw new InvalidOperationException("Starting monitor clipping failed.");
                results.Add("PASS: starting-monitor clipping");
                if (!SetWindowDisplayAffinity(cover.Handle, 0x11)) throw new InvalidOperationException("Capture exclusion affinity failed.");
                await Task.Delay(500);
                Check(await Capture("CaptureRectangleAsync", region, monitor), Color.Lime, "excluded-ui");
                try { await Capture("CaptureWindowAsync", (nint)0); throw new InvalidOperationException("Invalid target accepted."); }
                catch (ArgumentException) { results.Add("PASS: ordinary failure returned without process/batch loss"); }
                Check(retained, Color.Red, "retained-after-failure");
                Check(await Capture("CaptureWindowAsync", target.Handle), Color.Lime, "recovery");
                cover.Hide(); target.Activate(); await Task.Delay(500);
                var originalPointer = PhysicalPointer();
                try
                {
                    var inside = target.PointToScreen(new Point(target.ClientSize.Width / 2, target.ClientSize.Height / 2));
                    var pointerRegion = new Rectangle(inside - new Size(40, 40), new Size(80, 80));
                    var crop = pointerRegion; crop.Offset(-monitor.Left, -monitor.Top);
                    async Task<byte[]> Diagnostic(bool screen, bool cursor, Action? before = null) => await Capture("CaptureForProbeAsync", screen ? MonitorFromWindow(target.Handle, 2) : target.Handle, screen, cursor, before!, screen ? crop : null!);
                    int Differences(byte[] first, byte[] second, bool wholeImage)
                    {
                        using var aStream = new MemoryStream(first); using var bStream = new MemoryStream(second);
                        using var a = new Bitmap(aStream); using var b = new Bitmap(bStream);
                        if (a.Size != b.Size) throw new InvalidOperationException("Cursor comparison dimensions changed.");
                        var sample = wholeImage ? new Rectangle(Point.Empty, a.Size) : new Rectangle(a.Width / 2 - 40, a.Height / 2 - 40, 80, 80);
                        int differences = 0;
                        for (int y = sample.Top; y < sample.Bottom; y++)
                            for (int x = sample.Left; x < sample.Right; x++)
                                if (a.GetPixel(x, y) != b.GetPixel(x, y)) differences++;
                        return differences;
                    }
                    foreach (bool screen in new[] { false, true })
                    {
                        await RunCheck(screen ? "cursor-free-rectangle" : "cursor-free-window", async () =>
                        {
                            MovePointer(new Point(monitor.Right - 20, monitor.Top + 20)); await Task.Delay(250);
                            var baseline = await Diagnostic(screen, false);
                            MovePointer(inside); await Task.Delay(250);
                            if (PhysicalPointer() != inside || !pointerRegion.Contains(PhysicalPointer())) throw new InvalidOperationException("Physical pointer is outside sampled crop.");
                            results.Add($"Cursor mapping: screen={screen}; physical pointer={PhysicalPointer()}; monitor={monitor}; physical crop={crop}");
                            var clean = await Diagnostic(screen, false);
                            var positive = await Diagnostic(screen, true);
                            if (PhysicalPointer() != inside) throw new InvalidOperationException("Physical pointer moved during comparison.");
                            int cleanDifference = Differences(baseline, clean, screen), positiveDifference = Differences(clean, positive, screen);
                            string name = screen ? "cursor-free-rectangle" : "cursor-free-window";
                            File.WriteAllBytes(Path.Combine(evidence, name + "-baseline.png"), baseline);
                            File.WriteAllBytes(Path.Combine(evidence, name + ".png"), clean);
                            File.WriteAllBytes(Path.Combine(evidence, name + "-positive.png"), positive);
                            if (cleanDifference != 0 || positiveDifference == 0) throw new InvalidOperationException($"Cursor comparison failed: clean={cleanDifference}, enabled={positiveDifference}");
                            results.Add($"PASS: {name}; pointer inside; clean differing pixels={cleanDifference}; cursor-enabled differing pixels={positiveDifference}");
                        });
                    }
                    // Force the pool to use the old dimensions, then grow before
                    // StartCapture. The first old-size surface must never escape.
                    var oldSize = target.Size;
                    await RunCheck("resize-during-capture", async () =>
                    {
                        var resized = await Diagnostic(false, false, () => { target.Size = oldSize + new Size(160, 120); target.Refresh(); });
                        using (var stream = new MemoryStream(resized))
                        using (var image = new Bitmap(stream))
                        {
                            var expected = await Capture("CaptureWindowAsync", target.Handle);
                            using var expectedStream = new MemoryStream(expected); using var expectedImage = new Bitmap(expectedStream);
                            if (image.Size != expectedImage.Size || image.Width <= oldSize.Width || image.Height <= oldSize.Height)
                                throw new InvalidOperationException($"Truncated resize frame: {image.Size}, stable={expectedImage.Size}, old={oldSize}");
                            if (image.GetPixel(image.Width - 30, image.Height - 30).ToArgb() != Color.Lime.ToArgb())
                                throw new InvalidOperationException("Growing frame lost new lower-right content.");
                        }
                        Check(resized, Color.Lime, "resize-during-capture");
                    });
                    await RunCheck("shrink-during-capture", async () =>
                    {
                        target.Size = oldSize + new Size(160, 120); target.Refresh(); await Task.Delay(250);
                        var shrunk = await Diagnostic(false, false, () => { target.Size = oldSize; target.Refresh(); });
                        using (var stream = new MemoryStream(shrunk))
                        using (var image = new Bitmap(stream))
                        {
                            var stable = await Capture("CaptureWindowAsync", target.Handle);
                            using var stableStream = new MemoryStream(stable); using var stableImage = new Bitmap(stableStream);
                            if (image.Size != stableImage.Size) throw new InvalidOperationException("Shrinking frame dimensions differ from stable capture.");
                        }
                        Check(shrunk, Color.Lime, "shrink-during-capture");
                    });
                    await RunCheck("constructor failure after native acquisition", async () =>
                    {
                        try { await Diagnostic(false, false, () => throw new InvalidOperationException("Injected start failure")); throw new Exception("Failure injection did not run"); }
                        catch (InvalidOperationException error) when (error.Message == "Injected start failure") { results.Add("PASS: constructor failure after native acquisition contained"); }
                    });
                    await RunCheck("constructor-failure-recovery", async () =>
                        Check(await Capture("CaptureWindowAsync", target.Handle), Color.Lime, "constructor-failure-recovery"));
                }
                finally { MovePointer(originalPointer); }
                results.Add("UNTESTED: multi-monitor, mixed DPI, hardware/driver metadata");
                Environment.ExitCode = failed ? 1 : 0;
            }
            catch (Exception error) { results.Add("FAIL: " + error); Environment.ExitCode = 1; }
            finally
            {
                try { File.WriteAllLines(Path.Combine(evidence, "result.txt"), results); }
                finally { target.Close(); }
            }
        };
        Application.Run(target);
    }
}

