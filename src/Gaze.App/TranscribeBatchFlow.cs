using Gaze.Core;
namespace Gaze.App;

// UI-thread coordinator; native clipboard and window lifetime are injected boundaries.
internal sealed class TranscribeBatchFlow(TranscribeSession session, Action<IReadOnlyList<TranscribeCapture>> publish,
    Action<IReadOnlyList<TranscribeCapture>> zip, Action<bool> close)
{
    private bool publishing;
    private bool retryArchive;
    internal TranscribeSession Session => session;
    internal string Status { get; private set; } = "Clipboard unchanged";
    internal bool NeedsRetry { get; private set; }
    internal bool Closed { get; private set; }
    internal int Generation { get; private set; }
    internal bool CanZip => !Closed && !publishing && session.Count > 0;
    internal bool Accept(byte[] bytes, string title, int generation)
    {
        if (Closed || publishing || generation != Generation) return false;
        if (!session.AddCapture(bytes, title))
        {
            Status = session.LastCaptureRejection is TranscribeCaptureRejection.CountLimit or TranscribeCaptureRejection.ByteLimit
                ? $"Batch limit: {session.Limits.MaxCount} images / {(session.Limits.MaxPngBytes == 29_000_000 ? "29 MB" : "256 MiB")}. Use Next Batch or ZIP before capturing more."
                : "Capture rejected: no usable image. Try capturing again.";
            return false;
        }
        retryArchive = false; Retry(); return true;
    }
    internal void Undo() { if (Closed || publishing) return; Generation++; session.Undo(); retryArchive = false; Retry(); }
    internal void NextBatch() { if (Closed || publishing) return; Generation++; session.NextBatch(); retryArchive = false; Retry(); }
    internal void Retry() => Publish(retryArchive);
    internal void Zip() { if (CanZip) Publish(true); }
    private void Publish(bool archive)
    {
        if (Closed || publishing) return;
        retryArchive = archive;
        publishing = true;
        bool success = false;
        try
        {
            if (archive) zip(session.Captures); else publish(session.Captures);
            NeedsRetry = false;
            Status = session.Count == 0 ? "Batch empty; owned clipboard offering cleared" : "Clipboard current";
            success = true;
        }
        catch (Exception error) { NeedsRetry = true; Status = "Clipboard error; retry current batch. " + error.Message; }
        finally { publishing = false; }
        if (archive && success) { Closed = true; Generation++; close(true); }
    }
    internal void Close()
    {
        if (Closed || publishing) return;
        Closed = true; Generation++; close(false);
    }
}
