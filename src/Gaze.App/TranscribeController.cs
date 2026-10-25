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
    private int captureVersion;
    internal TranscribeWindow WindowForValidation => window;
    internal void CaptureForValidation(TranscribeSelection selection) => Capture(selection, selection.Area);
    internal void NextBatchForValidation() => NextBatch();
    internal void RetryForValidation() => RetryClipboard();
    internal int Count => session.Count;
    internal bool IsBusy => busy;
    internal bool HasGestureForValidation => gesture != null;
    internal void UndoForValidation() => Undo();
    internal void RenderEvidence(string path) => window.RenderEvidence(path);
    internal IReadOnlyList<TranscribeCapture> Captures => session.Captures;
    internal TranscribeController(bool autoCopy, string folder, Action<Action> dispatch, Action<TranscribeExporter, bool> completed, Action<IReadOnlyList<TranscribeCapture>>? export = null, TranscribeSession? initialSession = null, bool claudeDesktopClipboard = true, Action<IReadOnlyList<TranscribeCapture>>? publish = null, Func<TranscribeSelection, Rectangle, Task<byte[]>>? captureSource = null, Rectangle? placement = null, TranscribeTrigger trigger = TranscribeTrigger.MiddleMouse, Func<Action<TranscribePointer>, TranscribeInput>? inputFactory = null)
    {
        session = initialSession ?? new TranscribeSession(claudeDesktopClipboard);
        this.dispatch = dispatch; this.completed = completed;
        this.captureSource = captureSource ?? ((selection, area) => selection.IsWindow
            ? TranscribeScreenshotSource.CaptureWindowAsync(selection.Window)
            : TranscribeScreenshotSource.CaptureRectangleAsync(selection.Area, area));
        exporter = new TranscribeExporter(true, folder);
        flow = new TranscribeBatchFlow(session, publish ?? exporter.PublishBatch,
            export ?? (captures => exporter.PublishBatchZip(captures, claudeDesktopClipboard)), Complete, new OutputStore(folder));
        window = new TranscribeWindow(Undo, Finish, RequestClose, NextBatch, RetryClipboard, trigger);
        try
        {
            Action<TranscribePointer> queue = pointer => this.dispatch(() => HandleInput(pointer));
            input = inputFactory?.Invoke(queue) ?? new TranscribeInput(queue, trigger);
            PlaceWindow(placement);
            window.Show();
            PanelWindow.QueueOnHost(() => PlaceWindow(placement));
            if (!Native.SetWindowDisplayAffinity(window.NativeHandle, 0x11))
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Could not exclude Transcribe window from capture.");
            Refresh();
        }
        catch { disposed = true; input?.Dispose(); window.CloseAfterFinish(); overlay.Dispose(); exporter.Dispose(); throw; }
    }
    private void PlaceWindow(Rectangle? placement)
    {
        if (disposed || closing) return;
        var area = Screen.FromPoint(placement?.Location ?? Cursor.Position).WorkingArea;
        var handle = window.NativeHandle;
        var bounds = Native.WindowBounds(handle);
        // Adopt the target monitor's DPI before final sizing.
        Native.SetWindowPos(handle, 0, placement?.Left ?? area.Right - bounds.Width,
            placement?.Top ?? area.Bottom - bounds.Height, 0, 0, 0x15);
        var dpi = Native.GetDpiForWindow(handle) / 96.0;
        window.MaxWidth = area.Width / dpi; window.MaxHeight = area.Height / dpi;
        window.UpdateLayout();
        bounds = Native.WindowBounds(handle);
        int width = Math.Min(bounds.Width, area.Width), height = Math.Min(bounds.Height, area.Height);
        int x = Math.Clamp(placement?.Left ?? area.Right - width, area.Left, area.Right - width);
        int y = Math.Clamp(placement?.Top ?? area.Bottom - height, area.Top, area.Bottom - height);
        Native.SetWindowPos(handle, 0, x, y, width, height, 0x14);
    }
    internal void ShowWindow() { if (!disposed) window.BringToFront(); }
    private void Refresh(string? status = null) => window.Refresh(session, busy, status ?? flow.Status, flow.NeedsRetry);
    private void Undo() { if (!disposed && !closing) { CancelGesture(); flow.Undo(); Refresh(); } }
    private void NextBatch()
    {
        if (disposed || closing) return;
        CancelGesture();
        flow.NextBatch(); busy = false; Refresh();
    }
    private void RetryClipboard() { if (!busy) { flow.Retry(); Refresh(); } }
    internal void Finish()
    {
        if (disposed || busy || closing) return;
        CancelGesture();
        flow.Zip();
        if (!disposed) Refresh();
    }
    internal void Cancel() => RequestClose();
    internal bool RequestClose()
    {
        if (disposed) return true;
        CancelGesture();
        flow.Close();
        if (!disposed) Refresh();
        return disposed;
    }
    private void Complete(bool exported)
    {
        closing = true; CancelGesture(); input.Dispose(); overlay.Hide();
        window.CloseAfterFinish(); disposed = true; overlay.Dispose();
        completed(exporter, exported);
    }
    private void CancelGesture(bool cancelInput = true)
    {
        if (cancelInput) input.Cancel();
        captureVersion++; busy = false; gesture?.Cancel(); gesture = null; overlay.Hide();
    }
    private void HandleInput(TranscribePointer pointer)
    {
        if (pointer.Generation != input.Generation) return;
        if (pointer.Message == -1) { CancelGesture(false); return; }
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
        var generation = flow.Generation; var version = captureVersion;
        busy = true; Refresh();
        try
        {
            // Let DWM remove selection edges before requesting a fresh native frame.
            await Task.Delay(50);
            var bytes = await captureSource(selection, startingMonitor);
            dispatch(() =>
            {
                if (disposed || generation != flow.Generation || version != captureVersion) return;
                if (!flow.Accept(bytes, selection.Title, generation)) { Refresh(); return; }
                Refresh();
                overlay.Show(selection.IsWindow ? Native.WindowBounds(selection.Window) : selection.Area);
            });
            await Task.Delay(120);
            dispatch(() => { if (!disposed && generation == flow.Generation && version == captureVersion) { overlay.Hide(); busy = false; Refresh(); } });
        }
        catch (Exception error)
        {
            dispatch(() => { if (!disposed && generation == flow.Generation && version == captureVersion) { overlay.Hide(); busy = false; Refresh("Capture failed; previous captures retained. " + error.Message); } });
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CancelGesture(); input.Dispose(); overlay.Dispose(); window.CloseAfterFinish();
        exporter.Dispose();
    }
}
