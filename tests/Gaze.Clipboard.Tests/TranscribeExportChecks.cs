using System.IO.Compression;
using Gaze.App;
using Gaze.Core;

internal static class TranscribeExportChecks
{
    internal static void Run()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "export-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var s = new TranscribeSession(); s.AddCapture([1, 2, 3], "first");
        var folder = Path.Combine(root, "folder");
        File.WriteAllText(folder, "obstruction");
        using (var exporter = new TranscribeExporter(false, folder))
        {
            Check(!s.Finish(exporter.Export) && s.Count == 1, "Folder failure must retain batch");
            File.Delete(folder); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "existing.zip"), "preserve");
            Check(s.Finish(exporter.Export) && exporter.SavedPath != null, "Folder retry succeeds");
            using var zip = ZipFile.OpenRead(exporter.SavedPath!);
            Check(zip.GetEntry("000001_first.png") != null && File.ReadAllText(Path.Combine(folder, "existing.zip")) == "preserve", "Retried ZIP and existing file preserved");
        }
        int attempts = 0; string? staged = null; StagedZipOffer? offer = null;
        using (var exporter = new TranscribeExporter(true, folder, bytes => offer = new StagedZipOffer(bytes, root,
            _ => ++attempts == 1 ? unchecked((int)0x800401D0) : 0, _ => 0, () => 0)))
        {
            var clip = new TranscribeSession(); clip.AddCapture([4, 5], "clipboard");
            Check(!clip.Finish(exporter.Export) && clip.Count == 1, "Clipboard failure retains batch");
            staged = offer!.StagedPath;
            var modified = File.GetLastWriteTimeUtc(staged!);
            Check(clip.Finish(exporter.Export) && attempts == 2 && offer.StagedPath == staged && File.GetLastWriteTimeUtc(staged!) == modified, "Retry same staged ZIP without rewriting");
            using var zip = ZipFile.OpenRead(staged!);
            Check(zip.GetEntry("000001_clipboard.png") != null, "Staged route retains archive contents");
            Check(Directory.GetFiles(folder, "*.zip").Length == 2, "Clipboard route does not save in video folder");
        }
        Check(!File.Exists(staged), "Exporter lifetime cleanup removes its own staged ZIP");
        using (var unused = new TranscribeExporter(false, Path.Combine(root, "empty")))
        {
            new TranscribeSession().Finish(unused.Export);
            Check(!Directory.Exists(Path.Combine(root, "empty")), "Empty Finish creates nothing");
        }
        using (var emptyClipboard = new TranscribeExporter(true, folder, _ => throw new Exception("Empty clipboard session allocated offer")))
        {
            Check(!emptyClipboard.HasClipboardOffer, "Empty session has no clipboard lifetime to retain");
            new TranscribeSession().Finish(emptyClipboard.Export);
            Check(!emptyClipboard.HasClipboardOffer, "Empty Finish still has no clipboard lifetime");
        }
        Console.WriteLine("PASS export routes: folder failure/retry, collision preservation, staged clipboard retry/lifetime, no permanent clipboard copy, empty Finish");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
