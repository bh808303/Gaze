using System.IO.Compression;
using System.Text;
using Gaze.Core;

internal static class TranscribeArchiveChecks
{
    internal static void Run(Action<string, Action> test)
    {
        test("Transcribe archive retains PNG order, unique safe names and all instructions", () =>
        {
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/2kAAAAASUVORK5CYII=");
            var s = new TranscribeSession();
            foreach (var title in new[] { "CON", "", "bad<>:\"/\\|?*", "repeat", "repeat", new string('x', 300) }) s.AddCapture(png, title);
            s.AddCapture(png, "undone"); s.Undo(); s.AddCapture(png, "last");
            using var stream = new MemoryStream(TranscribeArchive.Create(s.Captures));
            using var zip = new ZipArchive(stream);
            var images = zip.Entries.Where(e => e.Name.EndsWith(".png")).ToArray();
            Check(images.Length == 7 && images.Select(e => e.Name).Distinct().Count() == 7, "One unique PNG per retained capture");
            Check(images[0].Name.StartsWith("000001_") && images[^1].Name.StartsWith("000008_"), "Capture order and Undo gaps preserved");
            foreach (var image in images)
            {
                Check(image.Name.Length < 150 && !image.Name.Any(c => "<>:\"/\\|?*".Contains(c)), "Portable safe leaf name");
                using var output = new MemoryStream(); using var input = image.Open(); input.CopyTo(output);
                Check(output.ToArray().SequenceEqual(png), "PNG bytes retained exactly");
                using var decoded = new ZLibStream(new MemoryStream(png, 41, 11), System.IO.Compression.CompressionMode.Decompress);
                using var pixels = new MemoryStream(); decoded.CopyTo(pixels);
                Check(pixels.Length == 3, "Fixture pixel scanline decodes");
            }
            using var reader = new StreamReader(zip.GetEntry("instructions.txt")!.Open(), Encoding.UTF8);
            var rules = reader.ReadToEnd();
            foreach (var rule in new[] { "order", "overlap", "duplicate", "heading", "unreadable", "guess" }) Check(rules.Contains(rule, StringComparison.OrdinalIgnoreCase), "Missing rule: " + rule);
        });
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
