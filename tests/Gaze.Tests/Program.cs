using Gaze.Core;

var failures = 0;
StartupChecks.Run(Test);
TranscribeChecks.Run(Test);
TranscribeArchiveChecks.Run(Test);
OptionalMetadataChecks.Run(Test);
Test("Mouse producers keep queuing while the drawing consumer is blocked, preserving order and clock times", () =>
{
    var queue = new Gaze.App.ClickQueue();
    var clock = DateTimeOffset.Now;
    using var entered = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    var received = new List<int>();
    queue.Enqueue("left", new System.Drawing.Point(0, 0), clock);
    var consumer = Task.Run(() => queue.Drain((button, point, time) =>
    {
        entered.Set(); release.Wait(); received.Add(point.X);
    }));
    try
    {
        if (!entered.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Consumer never started");
        var producer = Task.Run(() =>
        {
            for (int i = 1; i <= 1000; i++) queue.Enqueue("right", new System.Drawing.Point(i, 0), clock.AddMilliseconds(i));
        });
        if (!producer.Wait(TimeSpan.FromSeconds(1))) throw new Exception("Mouse producer blocked behind consumer");
    }
    finally { release.Set(); consumer.GetAwaiter().GetResult(); }
    queue.Drain((button, point, time) =>
    {
        Equal("right", button); Equal(clock.AddMilliseconds(point.X), time); received.Add(point.X);
    });
    Equal(true, received.SequenceEqual(Enumerable.Range(0, 1001)));
});
void Test(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
Test("Recording names include date, start/end, and video minutes across midnight", () =>
{
    var start = new DateTime(2026, 10, 10, 23, 59, 0);
    Equal("Gaze_d101026_t2359-0001_2min.mp4", RecordingName.Create(start, start.AddMinutes(2.5), TimeSpan.FromMinutes(2.5)));
    Equal("Gaze_d101026_t2359-0001_1min.mp4", RecordingName.Create(start, start.AddMinutes(2.5), TimeSpan.FromMinutes(1)));
    Equal("Gaze_d101026_t2359-2359_0min.mp4", RecordingName.Create(start, start.AddSeconds(30), TimeSpan.FromSeconds(30)));
    Equal(false, RecordingName.Create(start, start, TimeSpan.Zero).Contains(':'));
});
Test("Arming locks the original target", () =>
{
    var s = new Session(); s.Arm(CaptureMode.Window, 42); s.Start();
    Equal((nint)42, s.Root); Equal(SessionState.Recording, s.State);
});
Test("Current monitor locks its armed target and requires a monitor", () =>
{
    var s = new Session(); s.Arm(CaptureMode.CurrentMonitor, 77); s.Start();
    Equal((nint)77, s.Root); Equal(CaptureMode.CurrentMonitor, s.Mode);
    s.Stop(); Equal(SessionState.Saving, s.State);
    try { new Session().Arm(CaptureMode.CurrentMonitor, 0); throw new Exception("Missing monitor was accepted"); }
    catch (ArgumentException) { }
    Equal(1, (int)CaptureMode.AllMonitors);
});
Test("Pause excludes time from video duration", () =>
{
    var s = new Session(); s.Arm(CaptureMode.Window, 42); s.Start();
    s.Tick(TimeSpan.FromSeconds(3)); s.Pause(); s.Tick(TimeSpan.FromSeconds(20));
    s.Resume(); s.Tick(TimeSpan.FromSeconds(2)); Equal(TimeSpan.FromSeconds(5), s.Duration);
});
Test("Saving blocks rearming until completion", () =>
{
    var s = new Session(); s.Arm(CaptureMode.AllMonitors, 0); s.Start(); s.Stop();
    Equal(SessionState.Saving, s.State);
    try { s.Arm(CaptureMode.Window, 99); throw new Exception("Rearm was accepted"); }
    catch (InvalidOperationException) { }
    s.Reset(); Equal(SessionState.Idle, s.State);
});
Test("Window mode requires a target", () =>
{
    try { new Session().Arm(CaptureMode.Window, 0); throw new Exception("Missing window was accepted"); }
    catch (ArgumentException) { }
});
Test("Esc stops only on a separate press within one second", () =>
{
    var e = new EscapeConfirmation(); Equal(false, e.Press(TimeSpan.Zero));
    Equal(false, e.Press(TimeSpan.FromMilliseconds(100), true));
    Equal(true, e.Press(TimeSpan.FromMilliseconds(900)));
});
Test("Expired Esc starts a new confirmation interval", () =>
{
    var e = new EscapeConfirmation(); e.Press(TimeSpan.Zero);
    Equal(false, e.Press(TimeSpan.FromSeconds(1.1)));
    Equal(true, e.Press(TimeSpan.FromSeconds(1.5)));
});
Test("Reset clears an earlier Esc confirmation", () =>
{
    var e = new EscapeConfirmation(); e.Press(TimeSpan.Zero); e.Reset();
    Equal(false, e.Press(TimeSpan.FromMilliseconds(500)));
});
Test("Splitting preserves all frames in order and checks each part's size", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        var source = Path.Combine(folder, "recording.mp4"); File.WriteAllBytes(source, new byte[101]);
        var parts = Gaze.App.RecordingSplitter.SplitAsync(source, 11, 30, (start, count, path) =>
        {
            var data = new byte[count * 10];
            for (int i = 0; i < count; i++) data[i * 10] = (byte)(start + i);
            File.WriteAllBytes(path, data);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        Equal(false, File.Exists(source)); Equal(true, parts.Count > 1);
        // Each published filename contains its chronological part number.
        Equal("recording-part1.mp4", Path.GetFileName(parts[0]));
        Equal(11, parts.Sum(p => (int)new FileInfo(p).Length / 10));
        Equal(true, parts.All(p => new FileInfo(p).Length <= 30));
        Equal(string.Join(",", Enumerable.Range(0, 11)), string.Join(",", parts.SelectMany(p => File.ReadAllBytes(p).Where((_, i) => i % 10 == 0))));
    }
    finally { Directory.Delete(folder, true); }
});
Test("Recordings at the limit stay untouched", () =>
{
    var source = Path.GetTempFileName(); File.WriteAllBytes(source, new byte[30]);
    try
    {
        var parts = Gaze.App.RecordingSplitter.SplitAsync(source, 10, 30, (_, _, _) => throw new Exception("Unexpected encoding")).GetAwaiter().GetResult();
        Equal(source, parts.Single()); Equal(30L, new FileInfo(source).Length);
    }
    finally { File.Delete(source); }
});
Test("A split failure retains the original and publishes no incomplete parts", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder); var source = Path.Combine(folder, "recording.mp4"); File.WriteAllBytes(source, new byte[101]);
    try
    {
        try
        {
            Gaze.App.RecordingSplitter.SplitAsync(source, 11, 30, (first, _, output) =>
            {
                if (first > 0) throw new InvalidOperationException("Encoder failed after an accepted part");
                File.WriteAllBytes(output, new byte[20]); return Task.CompletedTask;
            }).GetAwaiter().GetResult();
            throw new Exception("Failure was ignored");
        }
        catch (InvalidOperationException) { }
        Equal(101L, new FileInfo(source).Length); Equal(1, Directory.GetFiles(folder).Length);
    }
    finally { Directory.Delete(folder, true); }
});
Test("Oversized single frames retain the original instead of silently exceeding the limit", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder); var source = Path.Combine(folder, "recording.mp4"); File.WriteAllBytes(source, new byte[101]);
    try
    {
        try
        {
            Gaze.App.RecordingSplitter.SplitAsync(source, 1, 30, (_, _, output) => { File.WriteAllBytes(output, new byte[40]); return Task.CompletedTask; }).GetAwaiter().GetResult();
            throw new Exception("Oversized frame was accepted");
        }
        catch (InvalidOperationException) { }
        Equal(101L, new FileInfo(source).Length); Equal(1, Directory.GetFiles(folder).Length);
    }
    finally { Directory.Delete(folder, true); }
});
Test("Part names never overwrite an existing recording", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder); var source = Path.Combine(folder, "recording.mp4"); File.WriteAllBytes(source, new byte[101]);
    var existing = Path.Combine(folder, "recording-part1.mp4"); File.WriteAllText(existing, "Existing recording");
    try
    {
        var parts = Gaze.App.RecordingSplitter.SplitAsync(source, 2, 30, (_, _, output) => { File.WriteAllBytes(output, new byte[20]); return Task.CompletedTask; }).GetAwaiter().GetResult();
        Equal("recording-2-part1.mp4", Path.GetFileName(parts[0])); Equal("Existing recording", File.ReadAllText(existing));
    }
    finally { Directory.Delete(folder, true); }
});
Test("Oversized parts are subdivided without losing or repeating frames", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder); var source = Path.Combine(folder, "recording.mp4"); File.WriteAllBytes(source, new byte[31]);
    try
    {
        var parts = Gaze.App.RecordingSplitter.SplitAsync(source, 11, 30, (first, count, output) =>
        {
            var bytes = new byte[count == 1 ? 20 : 40]; bytes[0] = (byte)first;
            File.WriteAllBytes(output, bytes); return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        Equal(11, parts.Count);
        Equal(string.Join(",", Enumerable.Range(0, 11)), string.Join(",", parts.Select(p => File.ReadAllBytes(p)[0])));
        Equal(true, parts.All(p => new FileInfo(p).Length <= 30));
    }
    finally { Directory.Delete(folder, true); }
});
TimelineChecks.Run(Test);
if (args.Length >= 2 && args[0] == "--encoder")
{
    foreach (var (fps, compression) in new[] { (1, CompressionMode.Original), (5, CompressionMode.Original), (30, CompressionMode.Compact) })
    Test($"Real MP4 splitting keeps every frame and each output at or below 29 MB ({fps} fps, {compression})", () =>
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Gaze-split-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            const int frames = 100, width = 800, height = 600;
            long DecodeBytes(string path)
            {
                var decode = new System.Diagnostics.ProcessStartInfo(args[1]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-i", path, "-an", "-pix_fmt", "bgra", "-fps_mode", "passthrough", "-f", "rawvideo", "pipe:1" }) decode.ArgumentList.Add(arg);
                using var process = System.Diagnostics.Process.Start(decode)!;
                var errors = process.StandardError.ReadToEndAsync(); var buffer = new byte[65536]; int read;
                long bytes = 0;
                while ((read = process.StandardOutput.BaseStream.Read(buffer)) != 0) bytes += read;
                process.WaitForExit();
                if (process.ExitCode != 0) throw new Exception(errors.GetAwaiter().GetResult());
                return bytes;
            }
            var source = Path.Combine(folder, "recording.mp4");
            var start = new System.Diagnostics.ProcessStartInfo(args[1]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{width}x{height}", "-framerate", fps.ToString(), "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-pix_fmt", "yuv420p", "-g", "10", "-movflags", "+frag_keyframe+empty_moov+default_base_moof", "-f", "mp4", source }) start.ArgumentList.Add(arg);
            using (var process = System.Diagnostics.Process.Start(start)!)
            {
                var errors = process.StandardError.ReadToEndAsync();
                var random = new Random(29); var pixels = new byte[width * height * 4];
                for (int i = 0; i < frames; i++) { random.NextBytes(pixels); process.StandardInput.BaseStream.Write(pixels); }
                process.StandardInput.Close(); process.WaitForExit();
                if (process.ExitCode != 0) throw new Exception(errors.GetAwaiter().GetResult());
            }
            Equal(true, new FileInfo(source).Length > Gaze.App.RecordingSplitter.LimitBytes);
            var originalBytes = DecodeBytes(source);
            Console.WriteLine($"  Original decodes to {originalBytes / (width * height * 4)} frames.");
            Equal((long)frames * width * height * 4, originalBytes);
            var eventTimeline = new RecordingTimeline(fps);
            var eventStart = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(2));
            eventTimeline.Window("fixture", "Large recording", "1", eventStart);
            for (int i = 0; i < frames; i++) { eventTimeline.Frame(eventStart.AddSeconds(i / (double)fps)); eventTimeline.Click("left", i, i, i, i, eventStart.AddSeconds(i / (double)fps).AddMilliseconds(1)); }
            var snapshot = eventTimeline.Snapshot(frames);
            var parts = Gaze.App.RecordingSplitter.SplitAsync(source, frames, Gaze.App.RecordingSplitter.LimitBytes, async (first, count, output) =>
            {
                int splitIndex = Array.IndexOf(args, "--split-encoder");
                var splitEncoder = splitIndex >= 0 ? args[splitIndex + 1] : args[1];
                await Gaze.App.RecordingSplitter.EncodeAsync(source, output, first, count, splitEncoder, fps, compression);
                if (!await Gaze.App.RecordingMetadata.TryEmbedAsync(output, snapshot.Slice(first, count), splitEncoder, message => throw new Exception(message)))
                    throw new Exception("Part metadata missing");
                Console.WriteLine($"  Range {first}+{count} decodes to {DecodeBytes(output) / (width * height * 4)} frames.");
            }).GetAwaiter().GetResult();
            Equal(true, parts.Count > 1); Equal(false, File.Exists(source));
            Equal(true, parts.All(p => new FileInfo(p).Length <= 29_000_000));
            long totalBytes = 0;
            foreach (var part in parts)
            {
                long partBytes = DecodeBytes(part);
                Equal(0L, partBytes % (width * height * 4)); Equal(true, partBytes > 0); totalBytes += partBytes;
            }
            Equal((long)frames * width * height * 4, totalBytes);
            Console.WriteLine($"  Verified {parts.Count} playable parts, {frames} frames, largest part {parts.Max(p => new FileInfo(p).Length)} bytes.");
        }
        finally { Directory.Delete(folder, true); }
    });
}
if (args.Contains("--metadata-tests"))
{
    int index = Array.IndexOf(args, "--metadata-tests");
    MetadataChecks.RunChecks(Test, args[index + 1], args[index + 2]);
}
Environment.ExitCode = failures == 0 ? 0 : 1;
