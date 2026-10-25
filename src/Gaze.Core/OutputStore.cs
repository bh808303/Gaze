namespace Gaze.Core;

public enum OutputCategory { Screenshots, Recordings }

// One store per capture session. Only identities/paths are cached, not image bytes.
public sealed class OutputStore(string root)
{
    private readonly HashSet<long> saved = [];
    private readonly HashSet<string> archives = [];
    public static string Folder(string root, OutputCategory category) => Path.Combine(Path.GetFullPath(root), category == OutputCategory.Screenshots ? "Screenshots" : "Videos");
    public void SaveCaptures(IReadOnlyList<TranscribeCapture> captures)
    {
        foreach (var capture in captures)
        {
            if (saved.Contains(capture.Sequence)) continue;
            Write($"Gaze_{capture.Sequence:000000}", ".png", capture.GetPngBytes());
            saved.Add(capture.Sequence);
        }
    }
    public void SaveZip(IReadOnlyList<TranscribeCapture> captures)
    {
        if (captures.Count == 0) return;
        var key = string.Join(",", captures.Select(c => c.Sequence));
        if (archives.Contains(key)) return;
        Write("Gaze_Transcribe", ".zip", TranscribeArchive.Create(captures));
        archives.Add(key);
    }
    private void Write(string name, string extension, byte[] bytes)
    {
        var directory = Folder(root, OutputCategory.Screenshots);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try { stream.Write(bytes); stream.Flush(true); }
        catch { stream.Dispose(); File.Delete(path); throw; }
    }
}
