using System.Diagnostics;
using Gaze.Core;

namespace Gaze.App;

internal static class RecordingSplitter
{
    internal const long LimitBytes = 29_000_000;

    internal static async Task<IReadOnlyList<string>> CreateAsync(string source, int frames, string encoder, int fps, CompressionMode compression,
        TimelineSnapshot? timeline = null, Action<string>? warning = null, long limit = LimitBytes)
    {
        warning ??= _ => { };
        bool metadataEnabled = timeline != null;
        // A small video can exceed the limit after metadata is added, so remux before
        // deciding whether it fits. Large recordings get metadata on their parts only.
        if (metadataEnabled && new FileInfo(source).Length <= limit)
            metadataEnabled = await RecordingMetadata.TryEmbedAsync(source, timeline!.Slice(0, frames), encoder, warning);
        return await SplitAsync(source, frames, limit, async (first, count, output) =>
        {
            await EncodeAsync(source, output, first, count, encoder, fps, compression);
            if (metadataEnabled) await RecordingMetadata.TryEmbedAsync(output, timeline!.Slice(first, count), encoder, warning);
        });
    }

    // Encode every range from the original, so retries never compound compression loss.
    internal static async Task<IReadOnlyList<string>> SplitAsync(string source, int frames, long limit, Func<int, int, string, Task> encode)
    {
        if (frames < 1 || limit < 1) throw new ArgumentOutOfRangeException(nameof(frames));
        var size = new FileInfo(source).Length;
        if (size <= limit) return new[] { source };
        var directory = Path.GetDirectoryName(Path.GetFullPath(source))!;
        var staging = Path.Combine(directory, ".gaze-split-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var accepted = new List<string>(); var published = new List<string>(); int attempt = 0;
        async Task Range(int first, int count)
        {
            var output = Path.Combine(staging, $"attempt-{++attempt}.mp4");
            await encode(first, count, output);
            var bytes = new FileInfo(output).Length;
            if (bytes == 0) throw new InvalidOperationException("Splitting produced an empty video.");
            if (bytes <= limit) { accepted.Add(output); return; }
            File.Delete(output);
            if (count == 1) throw new InvalidOperationException("A single video frame exceeds the file size limit. The original recording was retained.");
            int half = count / 2;
            await Range(first, half); await Range(first + half, count - half);
        }
        try
        {
            int chunk = Math.Max(1, (int)Math.Min(Math.Max(1, frames / 2), Math.Floor(frames * (double)limit / size * 0.85)));
            for (int first = 0; first < frames; first += Math.Min(chunk, frames - first))
                await Range(first, Math.Min(chunk, frames - first));
            var stem = Path.GetFileNameWithoutExtension(source);
            string[] destinations;
            for (int suffix = 1; ; suffix++)
            {
                var prefix = stem + (suffix == 1 ? "" : $"-{suffix}");
                destinations = Enumerable.Range(1, accepted.Count).Select(i => Path.Combine(directory, $"{prefix}-part{i}.mp4")).ToArray();
                if (!destinations.Any(File.Exists)) break;
            }
            for (int i = 0; i < accepted.Count; i++)
            {
                File.Move(accepted[i], destinations[i]); published.Add(destinations[i]);
            }
            // Only remove the original once all verified parts are published.
            File.Delete(source);
            return published;
        }
        catch
        {
            foreach (var path in published) File.Delete(path);
            throw;
        }
        finally
        {
            try { Directory.Delete(staging, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static async Task EncodeAsync(string source, string output, int first, int count, string encoder, int fps, CompressionMode compression)
    {
        await RecordingMetadata.RunAsync(encoder, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-map", "0:v:0", "-map_metadata", "-1", "-map_chapters", "-1", "-an", "-vf", $"trim=start_frame={first}:end_frame={first + count},setpts=PTS-STARTPTS", "-frames:v", count.ToString(), "-r", fps.ToString(), "-fps_mode", "cfr", "-c:v", "libx264", "-preset", compression == CompressionMode.Compact ? "medium" : "veryfast", "-crf", compression == CompressionMode.Compact ? "24" : "18", "-pix_fmt", "yuv420p", "-g", (fps * 2).ToString(), "-movflags", "+faststart", "-f", "mp4", output });
    }
}
