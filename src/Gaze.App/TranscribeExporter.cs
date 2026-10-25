using System.Runtime.InteropServices;
using Gaze.Core;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Gaze.App;

// One exporter per session freezes its route. The host retains clipboard exporters
// after the session window closes, until process exit. No sweeping of old files.
internal sealed class TranscribeExporter : IDisposable
{
    private readonly bool autoCopy;
    private readonly string folder;
    private TranscribeClipboardOwner? owner;
    private StagedZipOffer? offer;
    private readonly Func<byte[], StagedZipOffer>? createOffer;
    private bool disposed;
    private TranscribeBatchPublisher? batchPublisher;
    internal static string LiveRoot => Path.Combine(Path.GetTempPath(), "Gaze.Transcribe.Live");
    internal void PublishBatch(IReadOnlyList<TranscribeCapture> captures) => Live(p => p.Publish(captures));
    internal void PublishBatchZip(IReadOnlyList<TranscribeCapture> captures, bool limited) => Live(p => p.PublishZip(captures, limited));
    private void Live(Action<TranscribeBatchPublisher> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        owner ??= new TranscribeClipboardOwner();
        owner.Invoke(() =>
        {
            batchPublisher ??= new TranscribeBatchPublisher(LiveRoot, OleSetClipboard, OleIsCurrentClipboard, () => OleSetClipboard(null));
            action(batchPublisher);
        });
    }
    internal string? SavedPath { get; private set; }
    internal bool HasClipboardOffer => offer != null;

    internal TranscribeExporter(bool autoCopy, string folder, Func<byte[], StagedZipOffer>? createOffer = null)
    {
        this.autoCopy = autoCopy; this.folder = Path.GetFullPath(folder); this.createOffer = createOffer;
    }

    internal void Export(IReadOnlyList<TranscribeCapture> captures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (captures.Count == 0) return;
        if (autoCopy)
        {
            owner ??= new TranscribeClipboardOwner();
            if (offer == null)
            {
                var bytes = TranscribeArchive.Create(captures);
                offer = owner!.Invoke(() => createOffer?.Invoke(bytes) ?? new StagedZipOffer(bytes,
                    Path.Combine(Path.GetTempPath(), "Gaze.Transcribe"), OleSetClipboard,
                    OleIsCurrentClipboard, () => OleSetClipboard(null), referenced: IsReferenced, fileName: "Gaze_Transcribe.zip"));
            }
            owner!.Invoke(offer.Retry);
        }
        else
        {
            var bytes = TranscribeArchive.Create(captures);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Gaze_Transcribe_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.zip");
            // CreateNew refuses collisions. Failed writes remove only the file we opened.
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                try { output.Write(bytes); output.Flush(true); }
                catch { output.Dispose(); File.Delete(path); throw; }
            }
            SavedPath = path;
        }
    }

    internal void ResetPending()
    {
        if (offer == null) return;
        owner!.Invoke(offer.Stop); offer = null;
    }

    private static bool IsReferenced(string path) => Clipboard.ContainsFileDropList() &&
        Clipboard.GetFileDropList().Cast<string>().Any(value =>
            string.Equals(Path.GetFullPath(value), path, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        if (disposed) return;
        if (owner == null) { disposed = true; return; }
        // A clipboard/cleanup failure retains the owner and exact path for retry.
        if (batchPublisher != null) owner.Invoke(batchPublisher.Dispose);
        if (offer != null) owner.Invoke(offer.Stop);
        owner.Dispose();
        disposed = true;
    }
    [DllImport("ole32.dll")] private static extern int OleSetClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject? data);
    [DllImport("ole32.dll")] private static extern int OleIsCurrentClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject data);
}
