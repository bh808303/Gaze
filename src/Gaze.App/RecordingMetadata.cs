using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gaze.Core;

namespace Gaze.App;

internal static class RecordingMetadata
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    internal static string Chapters(TimelinePart part)
    {
        var text = new StringBuilder(";FFMETADATA1\n");
        text.Append("gaze_timeline=").Append(Escape(JsonSerializer.Serialize(part with { Events = [] }, Json))).Append('\n');
        string scope = part.Events.Any(e => e.Kind == "window" && e.Scope == "captured") ? "captured" : "foreground";
        var windows = part.Events.Where(e => e.Kind == "window" && e.Scope == scope).GroupBy(e => e.Frame).Select(g => g.Last()).OrderBy(e => e.Frame).ToArray();
        for (int i = 0; i < windows.Length; i++)
        {
            text.Append("[CHAPTER]\nTIMEBASE=1/").Append(part.FramesPerSecond)
                .Append("\nSTART=").Append(windows[i].Frame).Append("\nEND=").Append(i + 1 < windows.Length ? windows[i + 1].Frame : part.FrameCount)
                .Append("\ntitle=").Append(Escape((windows[i].External ? "Outside recorded area" : $"{windows[i].Process} — {windows[i].Title}").Replace('\r', ' ').Replace('\n', ' '))).Append('\n');
        }
        return text.ToString();
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("=", "\\=").Replace(";", "\\;").Replace("#", "\\#");
    internal static string Subtitles(TimelinePart part)
    {
        var text = new StringBuilder(); int index = 0;
        long next = 0;
        string Payload(IEnumerable<TimelineEvent> events)
        {
            var json = JsonSerializer.Serialize(new { version = 1, events }, Json);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return "gaze-base64-v1:" + string.Join("\n", Enumerable.Range(0, (encoded.Length + 119) / 120)
                .Select(i => encoded.Substring(i * 120, Math.Min(120, encoded.Length - i * 120))));
        }
        void Cue(int frame, List<TimelineEvent> batch)
        {
            var payload = Payload(batch);
            long first = Math.Max(next, (long)frame * 1000 / part.FramesPerSecond);
            next = first + 1;
            if (next > (long)part.FrameCount * 1000 / part.FramesPerSecond)
                throw new InvalidOperationException("Event batches exceed available timed-text millisecond slots; plain video retained.");
            text.Append(++index).Append('\n').Append(Time(first)).Append(" --> ").Append(Time(next)).Append('\n').Append(payload).Append("\n\n");
        }
        foreach (var group in part.Events.GroupBy(e => e.Frame).OrderBy(g => g.Key))
        {
            var batch = new List<TimelineEvent>(); int bytes = 0;
            foreach (var item in group)
            {
                int size = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(item, Json));
                if (size > 40_000) throw new InvalidOperationException("A single event exceeds the timed-text budget.");
                if (bytes + size > 40_000 && batch.Count > 0) { Cue(group.Key, batch); batch.Clear(); bytes = 0; }
                batch.Add(item); bytes += size + 1;
            }
            if (batch.Count > 0) Cue(group.Key, batch);
        }
        return text.ToString();
    }
    private static string Time(long milliseconds) => $"{milliseconds / 3_600_000:00}:{milliseconds / 60_000 % 60:00}:{milliseconds / 1000 % 60:00},{milliseconds % 1000:000}";

    // Replace only after a successful remux; failed metadata must never discard video.
    internal static async Task<bool> TryEmbedAsync(string source, TimelinePart part, string encoder, Action<string> warning)
    {
        var prefix = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!, ".gaze-metadata-" + Guid.NewGuid().ToString("N"));
        var metadata = prefix + ".ffmeta"; var subtitles = prefix + ".srt"; var output = prefix + ".mp4";
        try
        {
            await File.WriteAllTextAsync(metadata, Chapters(part));
            await File.WriteAllTextAsync(subtitles, Subtitles(part));
            await RunAsync(encoder, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-f", "ffmetadata", "-i", metadata,
                "-f", "srt", "-i", subtitles, "-map", "0:v:0", "-map", "2:s:0", "-map_metadata", "1", "-map_chapters", "1", "-c:v", "copy", "-c:s", "mov_text",
                "-disposition:s:0", "0", "-metadata:s:s:0", "handler_name=Gaze events", "-metadata:s:s:0", "title=Gaze events",
                "-movflags", "+faststart+use_metadata_tags", "-f", "mp4", output });
            if (new FileInfo(output).Length == 0) throw new InvalidOperationException("Metadata rewrite produced an empty file.");
            Mp4EventTrack.Disable(output);
            File.Move(output, source, true);
            return true;
        }
        catch (Exception e)
        {
            warning($"Event metadata was not added to {Path.GetFileName(source)}; plain video retained. {e.Message}");
            return false;
        }
        finally
        {
            foreach (var path in new[] { metadata, subtitles, output })
            {
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
    internal static async Task RunAsync(string encoder, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(encoder) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            await process.WaitForExitAsync(timeout.Token);
            var error = await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException($"FFmpeg failed: {error}");
        }
        finally { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); } }
    }
}
