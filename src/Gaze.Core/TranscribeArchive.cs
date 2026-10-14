using System.IO.Compression;
using System.Text;

namespace Gaze.Core;

public static class TranscribeArchive
{
    public static byte[] Create(IReadOnlyList<TranscribeCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        if (captures.Count == 0) throw new ArgumentException("An empty batch has no archive.", nameof(captures));
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var capture in captures)
            {
                var title = new string(capture.Title.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
                if (title.Length == 0) title = "capture";
                if (title.Length > 100) title = title[..100];
                // Sequence prefix makes device titles harmless and repeated titles unique.
                using var entry = archive.CreateEntry($"{capture.Sequence:D6}_{title}.png").Open();
                entry.Write(capture.GetPngBytes());
            }
            using var instructions = new StreamWriter(archive.CreateEntry("instructions.txt").Open(), new UTF8Encoding(false));
            instructions.Write("Transcribe PNGs in filename order. Merge overlapping text between frames. Remove duplicates. Preserve headings. Flag unreadable text without guessing.");
        }
        return output.ToArray();
    }
}
