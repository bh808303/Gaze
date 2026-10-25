using System.Runtime.InteropServices;
using Gaze.Core;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
namespace Gaze.App;

// App lifetime, confined to the pumping OLE STA. Session/window closure must not dispose it.
internal sealed class TranscribeBatchPublisher : IDisposable
{
    private readonly string directory;
    private readonly FileStream lease;
    private readonly Func<OleDataObject, int> publish, current;
    private readonly Func<int> clear;
    private readonly Action<string, byte[]> write;
    private readonly List<string> files = [];
    // Weak keys avoid retaining PNG buffers after their batch/session is released.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TranscribeCapture, string> staged = new();
    private OleDataObject? offering;
    private bool disposed;
    private const string Marker = "Gaze.Transcribe.Live.v1";

    internal TranscribeBatchPublisher(string root, Func<OleDataObject, int> publish,
        Func<OleDataObject, int> current, Func<int> clear, Action<string, byte[]>? write = null)
    {
        root = Path.GetFullPath(root);
        EnsureOrdinary(root);
        Directory.CreateDirectory(root);
        directory = Path.Combine(root, "transcribe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        EnsureOrdinary(directory);
        lease = new FileStream(Path.Combine(directory, "owner"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        lease.Write(System.Text.Encoding.UTF8.GetBytes(Marker)); lease.Flush(true);
        this.publish = publish; this.current = current; this.clear = clear;
        this.write = write ?? ((path, bytes) =>
        {
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(bytes); output.Flush(true);
        });
    }

    // Always pass the current B01 snapshot, including empty snapshots for Undo/reset.
    // Exceptions mean clipboard status is stale; retain captures and retry with a NEW snapshot.
    internal void Publish(IReadOnlyList<TranscribeCapture> snapshot)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (snapshot.Count == 0) { ClearOwned(); return; }
        var paths = new List<string>();
        foreach (var capture in snapshot)
        {
            if (!staged.TryGetValue(capture, out var path))
            {
                path = Stage(Name(capture), capture.GetPngBytes());
                // Cache only completed writes. A partial write is journaled, never reused.
                staged.Add(capture, path);
            }
            paths.Add(path);
        }
        PublishPaths(paths);
    }

    internal void PublishZip(IReadOnlyList<TranscribeCapture> snapshot, bool limited)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (snapshot.Count == 0) throw new InvalidOperationException("An empty batch has no ZIP.");
        var bytes = TranscribeArchive.Create(snapshot);
        if (limited && bytes.LongLength > 29_000_000) throw new IOException("ZIP exceeds 29,000,000 bytes including archive overhead.");
        PublishPaths([Stage("Gaze_Transcribe.zip", bytes)]);
    }

    private static string Name(TranscribeCapture capture)
    {
        var title = new string(capture.Title.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (title.Length == 0) title = "capture";
        if (title.Length > 100) title = title[..100];
        return $"{capture.Sequence:D6}_{title}.png";
    }

    private string Stage(string name, byte[] bytes)
    {
        EnsureOrdinary(directory);
        var batch = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(batch);
        EnsureOrdinary(batch);
        var path = Path.Combine(batch, name);
        files.Add(Path.GetRelativePath(directory, path));
        var journal = Path.Combine(directory, "files");
        EnsureOrdinary(journal);
        File.WriteAllLines(journal, files);
        write(path, bytes);
        return path;
    }

    private void PublishPaths(IReadOnlyList<string> paths)
    {
        var data = new DataObject();
        var drop = new System.Collections.Specialized.StringCollection(); drop.AddRange(paths.ToArray());
        data.SetFileDropList(drop);
        Marshal.ThrowExceptionForHR(publish((OleDataObject)data));
        offering = (OleDataObject)data;
    }

    internal void ClearOwned()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offering == null) return;
        var result = current(offering);
        Marshal.ThrowExceptionForHR(result);
        if (result == 0) Marshal.ThrowExceptionForHR(clear());
        else if (result != 1) throw new IOException("Clipboard ownership is uncertain.");
        offering = null;
    }

    // Return contained failures for the host to report. Active leases are never reclaimed.
    internal static IReadOnlyList<string> CleanupStale(string root)
    {
        var errors = new List<string>();
        try
        {
            root = Path.GetFullPath(root); EnsureOrdinary(root);
            if (!Directory.Exists(root)) return errors;
            foreach (var candidate in Directory.EnumerateDirectories(root, "transcribe-*"))
            {
                if (!Guid.TryParseExact(Path.GetFileName(candidate)[11..], "N", out _)) continue;
                try
                {
                    EnsureOrdinary(candidate);
                    var owner = Path.Combine(candidate, "owner"); EnsureOrdinary(owner);
                    using (var handle = new FileStream(owner, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using var reader = new StreamReader(handle, leaveOpen: true);
                        if (reader.ReadToEnd() != Marker) continue;
                        CleanupFiles(candidate);
                    }
                    File.Delete(owner);
                    if (!Directory.EnumerateFileSystemEntries(candidate).Any()) Directory.Delete(candidate);
                }
                catch (Exception error) { errors.Add(candidate + ": " + error.Message); }
            }
        }
        catch (Exception error) { errors.Add(error.Message); }
        return errors;
    }

    private static void CleanupFiles(string directory)
    {
        EnsureOrdinary(directory);
        var journal = Path.Combine(directory, "files"); EnsureOrdinary(journal);
        var owned = File.Exists(journal) ? File.ReadAllLines(journal) : [];
        // Validate the entire journal before deleting anything. No recursive traversal.
        foreach (var relative in owned)
        {
            var parts = relative.Split(Path.DirectorySeparatorChar);
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out _) ||
                Path.GetFileName(parts[1]) != parts[1] ||
                !(parts[1].EndsWith(".png", StringComparison.Ordinal) || parts[1] == "Gaze_Transcribe.zip"))
                throw new IOException("Invalid artifact journal.");
            EnsureOrdinary(Path.Combine(directory, relative));
        }
        foreach (var relative in owned)
        {
            var path = Path.Combine(directory, relative); File.Delete(path);
            var batch = Path.GetDirectoryName(path)!;
            if (Directory.Exists(batch) && !Directory.EnumerateFileSystemEntries(batch).Any()) Directory.Delete(batch);
        }
        File.Delete(journal);
    }

    private static void EnsureOrdinary(string path)
    {
        for (var item = new FileInfo(Path.GetFullPath(path)); item != null; item = item.Directory == null ? null : new FileInfo(item.Directory.FullName))
            if ((File.Exists(item.FullName) || Directory.Exists(item.FullName)) && (File.GetAttributes(item.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing redirected artifact path: " + item.FullName);
    }

    public void Dispose()
    {
        if (disposed) return;
        ClearOwned();
        CleanupFiles(directory);
        lease.Dispose();
        File.Delete(Path.Combine(directory, "owner"));
        if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        disposed = true;
    }
}
