using System.Diagnostics;
using System.Text.Json;
using Gaze.App;
using Gaze.Core;

internal static class MetadataChecks
{
    private static string Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000)) { process.Kill(); throw new Exception("Metadata test timed out"); }
        if (process.ExitCode != 0) throw new Exception(errors.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void RunChecks(Action<string, Action> test, string encoder, string probe)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "metadata-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var plain = Path.Combine(folder, "plain.mp4"); const int frames = 12, fps = 5;
        var startTime = new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.FromHours(2));
        const string title = "C:\\New folder\\notes.txt - Notepad | Line\\nbreak | Line\\Nbreak | real\nnewline | <b> 😀";
        var timeline = new RecordingTimeline(fps); timeline.Window("mail", title, "1", startTime);
        for (int i = 0; i < frames; i++)
        {
            if (i == 6) { timeline.Pause(startTime.AddSeconds(1.1)); timeline.Window("browser", "Ticket", "2", startTime.AddSeconds(9)); timeline.Resume(startTime.AddSeconds(10)); }
            timeline.Frame(startTime.AddSeconds(i / (double)fps + (i >= 6 ? 10 : 0)));
            timeline.Click("left", 100 + i, 200 + i, i, i, startTime.AddSeconds(i / (double)fps + (i >= 6 ? 10 : 0)).AddMilliseconds(30));
        }
        var snapshot = timeline.Snapshot(frames); var raw = Path.Combine(folder, "frames.bgra");
        var random = new Random(2); var bytes = new byte[160 * 120 * 4 * frames]; random.NextBytes(bytes); File.WriteAllBytes(raw, bytes);
        try
        {
            Run(encoder, "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "160x120", "-framerate", "5", "-i", raw,
                "-an", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-f", "mp4", plain);
            string Hashes(string file)
            {
                using var data = JsonDocument.Parse(Run(probe, "-v", "error", "-select_streams", "v:0", "-show_packets", "-show_data_hash", "sha256", "-of", "json", file));
                return string.Join("|", data.RootElement.GetProperty("packets").EnumerateArray().Select(p => p.GetProperty("data_hash").GetString()));
            }
            JsonDocument Read(string file) => JsonDocument.Parse(Run(Environment.GetEnvironmentVariable("GAZE_TEST_PYTHON") ?? "python", "scripts/Read-Timeline.py", file, "--probe", probe));
            var embedded = Path.Combine(folder, "embedded.mp4"); File.Copy(plain, embedded);
            test("MP4 metadata rewrite preserves encoded video bytes, chapters, titles and an off-by-default text track", () =>
            {
                var before = Hashes(plain);
                Require(RecordingMetadata.TryEmbedAsync(embedded, snapshot.Slice(0, frames), encoder, message => throw new Exception(message)).GetAwaiter().GetResult(), "Embedding failed");
                Require(Hashes(embedded) == before, "Metadata rewrite re-encoded or lost video packets");
                using var info = JsonDocument.Parse(Run(probe, "-v", "error", "-show_streams", "-of", "json", embedded));
                var track = info.RootElement.GetProperty("streams").EnumerateArray().Single(s => s.GetProperty("codec_name").GetString() == "mov_text");
                Require(track.GetProperty("disposition").GetProperty("default").GetInt32() == 0, "Event track defaults to on");
                using var data = Read(embedded); var root = data.RootElement;
                Require(root.GetProperty("events").EnumerateArray().Count(e => e.GetProperty("kind").GetString() == "click") == frames, "Events were lost in the text track");
                Require(root.GetProperty("events").EnumerateArray().First(e => e.GetProperty("kind").GetString() == "window").GetProperty("title").GetString() == title, "Event title was altered");
                Require(root.GetProperty("chapters")[0].GetProperty("title").GetString() == "mail — " + title.Replace('\n', ' '), "Chapter title was altered");
                Require(root.GetProperty("chapters").GetArrayLength() == 2, "Window chapters are missing");
                Require(root.GetProperty("clockAnchors")[1].GetProperty("frame").GetInt32() == 6, "Resume anchor is missing");
            });
            test("Failed metadata rewrite keeps the plain video and logs a warning", () =>
            {
                var before = File.ReadAllBytes(plain); var warnings = new List<string>();
                Require(!RecordingMetadata.TryEmbedAsync(plain, snapshot.Slice(0, frames), Path.Combine(folder, "missing-encoder.exe"), warnings.Add).GetAwaiter().GetResult(), "Failure was not reported");
                Require(before.SequenceEqual(File.ReadAllBytes(plain)) && warnings.Count == 1, "Plain video was changed or warning was lost");
            });
            test("Size checking includes metadata and split parts retain every event with local timestamps", () =>
            {
                long plainSize = new FileInfo(plain).Length, embeddedSize = new FileInfo(embedded).Length;
                Require(embeddedSize > plainSize + 50, "Fixture must grow after adding metadata");
                long limit = (plainSize + embeddedSize) / 2;
                var source = Path.Combine(folder, "split.mp4"); File.Copy(plain, source);
                var warnings = new List<string>();
                var parts = RecordingSplitter.CreateAsync(source, frames, encoder, fps, CompressionMode.Original, snapshot, warnings.Add, limit).GetAwaiter().GetResult();
                Require(parts.Count > 1 && warnings.Count == 0, "Metadata did not trigger splitting or part embedding failed");
                var clicks = new List<int>(); int count = 0;
                foreach (var part in parts)
                {
                    Require(new FileInfo(part).Length <= limit, "Metadata pushed a part beyond the limit");
                    using var data = Read(part); var root = data.RootElement;
                    int first = root.GetProperty("recordingFirstFrame").GetInt32(), length = root.GetProperty("frameCount").GetInt32(); count += length;
                    Require(root.GetProperty("partStartTime").GetDateTimeOffset() == snapshot.FrameTimes[first], "Part clock time was estimated");
                    Require(root.GetProperty("chapters")[0].GetProperty("startSeconds").GetDouble() == 0, "Active window chapter was not carried over");
                    foreach (var e in root.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "click"))
                    {
                        int originalFrame = e.GetProperty("recordingFrame").GetInt32(); clicks.Add(originalFrame);
                        Require(e.GetProperty("frame").GetInt32() == originalFrame - first, "Event timestamp was not rebased");
                    }
                }
                Require(count == frames && clicks.SequenceEqual(Enumerable.Range(0, frames)), "Split events were lost, duplicated or reordered");
            });
        }
        finally { Directory.Delete(folder, true); }
    }
}
