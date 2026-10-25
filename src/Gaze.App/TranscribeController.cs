using Gaze.Core;
using Gaze.Fluent;

namespace Gaze.App;

internal sealed class TranscribeController : IDisposable
{
    private readonly TranscribeSession session;
    private readonly TranscribeWindow window;
    private readonly TranscribeOverlay overlay = new();
    private readonly TranscribeExporter exporter;
    private readonly Action<TranscribeExporter, bool> completed;
    private readonly Action<Action> dispatch;
    private readonly TranscribeBatchFlow flow;
    private readonly TranscribeInput input;
    private readonly Func<TranscribeSelection, Rectangle, Task<byte[]>> captureSource;
    private TranscribeGesture? gesture;
    private Rectangle monitor;
    private bool busy, disposed, closing;
    internal TranscribeWindow WindowForValidation => window;
    internal void CaptureForValidation(TranscribeSelection selection) => Capture(selection, selection.Area);
    internal void NextBatchForValidation() => NextBatch();
    internal void RetryForValidation() => RetryClipboard();
    internal int Count => session.Count;
    internal bool IsBusy => busy;
    internal void UndoForValidation() => Undo();
    internal void RenderEvidence(string path) => window.RenderEvidence(path);
    internal IReadOnlyList<TranscribeCapture> Captures => session.Captures;
    internal TranscribeController(bool autoCopy, string folder, Action<Action> dispatch, Action<TranscribeExporter, bool> completed, Action<IReadOnlyList<TranscribeCapture>>? export = null, TranscribeSession? initialSession = null, bool claudeDesktopClipboard = true, Action<IReadOnlyList<TranscribeCapture>>? publish = null, Func<TranscribeSelection, Rectangle, Task<byte[]>>? captureSource = null)
    {
        session = initialSession ?? new TranscribeSession(claudeDesktopClipboard);
        this.dispatch = dispatch; this.completed = completed;
        this.captureSource = captureSource ?? ((selection, area) => selection.IsWindow
            ? TranscribeScreenshotSource.CaptureWindowAsync(selection.Window)
            : TranscribeScreenshotSource.CaptureRectangleAsync(selection.Area, area));
        exporter = new TranscribeExporter(true, folder);
        flow = new TranscribeBatchFlow(session, publish ?? exporter.PublishBatch,
            export ?? (captures => exporter.PublishBatchZip(captures, claudeDesktopClipboard)), Complete);
        window = new TranscribeWindow(Undo, Finish, RequestClose, NextBatch, RetryClipboard);
        try
        {
            input = new TranscribeInput(pointer => this.dispatch(() => HandleInput(pointer)));
            window.Show();
            if (!Native.SetWindowDisplayAffinity(window.NativeHandle, 0x11))
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Could not exclude Transcribe window from capture.");
            Refresh();
        }
        catch { input?.Dispose(); window.CloseAfterFinish(); overlay.Dispose(); exporter.Dispose(); throw; }
    }
    private void Refresh(string? status = null) => window.Refresh(session, busy, status ?? flow.Status, flow.NeedsRetry);
    private void Undo() { if (!busy) { flow.Undo(); Refresh(); } }
    private void NextBatch()
    {
        if (disposed || closing) return;
        gesture?.Cancel(); gesture = null; overlay.Hide();
        flow.NextBatch(); busy = false; Refresh();
    }
    private void RetryClipboard() { if (!busy) { flow.Retry(); Refresh(); } }
    internal void Finish()
    {
        if (disposed || busy || closing) return;
        gesture?.Cancel(); gesture = null; overlay.Hide();
        flow.Zip();
        if (!disposed) Refresh();
    }
    internal void Cancel() => RequestClose();
    internal bool RequestClose()
    {
        if (disposed) return true;
        flow.Close();
        return disposed;
    }
    private void Complete(bool exported)
    {
        closing = true; gesture?.Cancel(); gesture = null; input.Dispose(); overlay.Hide();
        window.CloseAfterFinish(); disposed = true; overlay.Dispose();
        completed(exporter, exported);
    }
    private void HandleInput(TranscribePointer pointer)
    {
        if (disposed || busy || closing || session.State != TranscribeState.Capturing) return;
        var message = pointer.Message; var point = pointer.Point;
        if (message == 0x207)
        {
            gesture?.Cancel(); overlay.Hide();
            if (pointer.OwnWindow) { gesture = null; return; }
            monitor = pointer.Monitor;
            gesture = new TranscribeGesture(point, SystemInformation.DragSize, pointer.Window, pointer.Title, monitor);
        }
        else if (message == 0x200 && gesture != null) overlay.Show(gesture.Preview(point));
        else if (message == 0x208 && gesture != null)
        {
            var selection = gesture.Complete(point); gesture = null; overlay.Hide();
            if (selection != null) Capture(selection, monitor);
        }
    }
    private async void Capture(TranscribeSelection selection, Rectangle startingMonitor)
    {
        var generation = flow.Generation;
        busy = true; Refresh();
        try
        {
            // Let DWM remove selection edges before requesting a fresh native frame.
            await Task.Delay(50);
            var bytes = await captureSource(selection, startingMonitor);
            dispatch(() =>
            {
                if (disposed || generation != flow.Generation) return;
                if (!flow.Accept(bytes, selection.Title, generation)) { Refresh(); return; }
                Refresh();
                overlay.Show(selection.IsWindow ? Native.WindowBounds(selection.Window) : selection.Area);
            });
            await Task.Delay(120);
            dispatch(() => { if (!disposed && generation == flow.Generation) { overlay.Hide(); busy = false; Refresh(); } });
        }
        catch (Exception error)
        {
            dispatch(() => { if (!disposed && generation == flow.Generation) { overlay.Hide(); busy = false; Refresh("Capture failed; previous captures retained. " + error.Message); } });
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        input.Dispose(); overlay.Dispose(); window.CloseAfterFinish(); disposed = true;
        exporter.Dispose();
    }
}
