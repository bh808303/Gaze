namespace Gaze.Core;

public enum TranscribeState { Capturing, Exporting, ExportFailed, Closed }
public enum TranscribeCloseChoice { Finish, Discard, KeepCapturing }

public sealed class TranscribeCapture
{
    private readonly byte[] png;
    internal TranscribeCapture(long sequence, string title, byte[] bytes)
    { Sequence = sequence; Title = title; png = (byte[])bytes.Clone(); }
    public long Sequence { get; }
    public string Title { get; }
    public byte[] GetPngBytes() => (byte[])png.Clone();
}

public sealed class TranscribeSession
{
    private readonly List<TranscribeCapture> captures = [];
    private long sequence;
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
        if (State != TranscribeState.Capturing || png.Length == 0) return false;
        var capture = new TranscribeCapture(checked(sequence + 1), title ?? "", png);
        captures.Add(capture);
        sequence = capture.Sequence;
        return true;
    }

    public bool Undo()
    {
        if (State != TranscribeState.Capturing || captures.Count == 0) return false;
        captures.RemoveAt(captures.Count - 1);
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
            captures.Clear();
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
                captures.Clear(); LastExportError = null; State = TranscribeState.Closed; return true;
            case TranscribeCloseChoice.KeepCapturing:
                LastExportError = null; State = TranscribeState.Capturing; return false;
            default: throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }
}
