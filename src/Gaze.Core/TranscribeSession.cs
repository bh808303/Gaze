namespace Gaze.Core;

public enum TranscribeState { Capturing, Exporting, ExportFailed, Closed }
public enum TranscribeCloseChoice { Finish, Discard, KeepCapturing }
public enum TranscribeCaptureRejection { None, NotCapturing, EmptyPng, CountLimit, ByteLimit }

public sealed record TranscribeBatchLimits(int MaxCount, long MaxPngBytes)
{
    public static TranscribeBatchLimits ClaudeDesktop { get; } = new(20, 29_000_000);
    public static TranscribeBatchLimits Standard { get; } = new(100, 268_435_456);
}

public sealed class TranscribeCapture
{
    private readonly byte[] png;
    internal TranscribeCapture(long sequence, string title, byte[] bytes)
    { Sequence = sequence; Title = title; png = (byte[])bytes.Clone(); }
    public long Sequence { get; }
    public string Title { get; }
    public long PngByteCount => png.LongLength;
    public byte[] GetPngBytes() => (byte[])png.Clone();
}

public sealed class TranscribeSession
{
    private readonly List<TranscribeCapture> captures = [];
    private long sequence;
    public TranscribeSession() : this(true) { }
    public TranscribeSession(bool claudeDesktopClipboard)
    { Limits = claudeDesktopClipboard ? TranscribeBatchLimits.ClaudeDesktop : TranscribeBatchLimits.Standard; }
    public TranscribeBatchLimits Limits { get; }
    public long TotalPngBytes { get; private set; }
    public TranscribeCaptureRejection LastCaptureRejection { get; private set; }
    public TranscribeState State { get; private set; }
    public Exception? LastExportError { get; private set; }
    public IReadOnlyList<TranscribeCapture> Captures => captures.AsReadOnly();
    public int Count => Captures.Count;
    public TranscribeCapture? Latest => captures.Count == 0 ? null : captures[^1];

    // The capture adapter validates geometry/PNG encoding before calling this method.
    // Failed captures must not call AddCapture. This model is confined to the UI thread.
    public bool AddCapture(byte[] png, string title)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (State != TranscribeState.Capturing) return Reject(TranscribeCaptureRejection.NotCapturing);
        if (png.Length == 0) return Reject(TranscribeCaptureRejection.EmptyPng);
        // TODO B01: enforce count limit before retaining a capture.
        // TODO B01: enforce aggregate byte limit before retaining a capture.
        var capture = new TranscribeCapture(checked(sequence + 1), title ?? "", png);
        captures.Add(capture);
        sequence = capture.Sequence;
        TotalPngBytes += capture.PngByteCount;
        LastCaptureRejection = TranscribeCaptureRejection.None;
        return true;
    }

    private bool Reject(TranscribeCaptureRejection reason)
    { LastCaptureRejection = reason; return false; }

    public bool NextBatch()
    {
        if (State != TranscribeState.Capturing) return false;
        // TODO B01: reset current batch.
        return true;
    }

    private void ClearBatch()
    {
        captures.Clear();
        TotalPngBytes = 0;
        LastCaptureRejection = TranscribeCaptureRejection.None;
        LastExportError = null;
    }

    public bool Undo()
    {
        if (State != TranscribeState.Capturing || captures.Count == 0) return false;
        TotalPngBytes -= captures[^1].PngByteCount;
        captures.RemoveAt(captures.Count - 1);
        LastCaptureRejection = TranscribeCaptureRejection.None;
        return true;
    }

    // Done, Stop, and Retry use this same synchronous contract. Exporters return only
    // after successful publication/save, or throw; a failure must leave the UI open.
    public bool Finish(Action<IReadOnlyList<TranscribeCapture>> export)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (State == TranscribeState.Closed) return true;
        if (State == TranscribeState.Exporting) return false;
        State = TranscribeState.Exporting;
        try
        {
            if (captures.Count > 0) export(Array.AsReadOnly(captures.ToArray()));
            ClearBatch();
            LastExportError = null;
            State = TranscribeState.Closed;
            return true;
        }
        catch (Exception error)
        {
            LastExportError = error;
            State = TranscribeState.ExportFailed;
            return false;
        }
    }

    // Cancel uses Discard. Close/quit prompts supply the user's choice; callers may
    // close their window/process only when this returns true.
    public bool RequestClose(TranscribeCloseChoice choice, Action<IReadOnlyList<TranscribeCapture>> export)
    {
        if (State == TranscribeState.Closed) return true;
        if (State == TranscribeState.Exporting) return false;
        switch (choice)
        {
            case TranscribeCloseChoice.Finish: return Finish(export);
            case TranscribeCloseChoice.Discard:
                ClearBatch(); State = TranscribeState.Closed; return true;
            case TranscribeCloseChoice.KeepCapturing:
                LastExportError = null; State = TranscribeState.Capturing; return false;
            default: throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }
}
