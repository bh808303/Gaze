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
    private readonly Action<IReadOnlyList<TranscribeCapture>> export;
    private readonly TranscribeInput input;
    private TranscribeGesture? gesture;
    private Rectangle monitor;
    private bool busy, disposed, closing;
    internal int Count => session.Count;
    internal bool IsBusy => busy;
    internal void UndoForValidation() => Undo();
    internal void RenderEvidence(string path) => window.RenderEvidence(path);
    internal IReadOnlyList<TranscribeCapture> Captures => session.Captures;
    internal TranscribeController(bool autoCopy, string folder, Action<Action> dispatch, Action<TranscribeExporter, bool> completed, Action<IReadOnlyList<TranscribeCapture>>? export = null, TranscribeSession? initialSession = null)
    {
        session = initialSession ?? new TranscribeSession();
        this.dispatch = dispatch; this.completed = completed;
        exporter = new TranscribeExporter(autoCopy, folder);
        this.export = export ?? exporter.Export;
        window = new TranscribeWindow(Undo, Finish, Cancel, RequestClose);
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
    private void Refresh(string? status = null) => window.Refresh(session, busy, status);
    private void Undo() { if (!busy) { session.Undo(); Refresh(); } }
    internal void Finish()
    {
        if (disposed || busy || closing) return;
        gesture?.Cancel(); gesture = null; overlay.Hide();
        busy = true; Refresh("Exporting…");
        bool success;
        try { success = session.Finish(export); }
        finally { busy = false; }
        if (success) Complete(true);
        else Refresh("Export failed. Captures retained. " + session.LastExportError?.Message);
    }
    internal void Cancel()
    {
        if (busy || disposed) return;
        if (session.RequestClose(TranscribeCloseChoice.Discard, exporter.Export)) Complete(false);
    }
    internal bool RequestClose()
    {
        if (disposed) return true;
        if (busy || closing) return false;
        if (session.Count == 0) { Finish(); return disposed; }
        gesture?.Cancel(); gesture = null; overlay.Hide();
        using var prompt = new Form { Text = "Finish this Transcribe session?", ClientSize = new Size(440, 120), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, MinimizeBox = false, MaximizeBox = false };
        var label = new Label { Text = "Finish exports the captures. Discard removes them.", AutoSize = true, Left = 16, Top = 16 };
        var finish = new Button { Text = "Finish", Left = 16, Top = 65, Width = 110, DialogResult = DialogResult.Yes };
        var discard = new Button { Text = "Discard", Left = 136, Top = 65, Width = 110, DialogResult = DialogResult.No };
        var keep = new Button { Text = "Keep capturing", Left = 256, Top = 65, Width = 160, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange([label, finish, discard, keep]); prompt.CancelButton = keep;
        closing = true;
        DialogResult choice;
        try { choice = prompt.ShowDialog(); }
        finally { closing = false; }
        if (choice == DialogResult.Yes) Finish();
        else if (choice == DialogResult.No) Cancel();
        else
        {
            try { exporter.ResetPending(); session.RequestClose(TranscribeCloseChoice.KeepCapturing, exporter.Export); Refresh(); }
            catch (Exception error) { Refresh("Could not resume capture: " + error.Message); }
        }
        return disposed;
    }
    private void Complete(bool exported)
    {
        closing = true; input.Dispose(); overlay.Hide();
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
        busy = true; Refresh("Capturing…");
        try
        {
            // Let DWM remove selection edges before requesting a fresh native frame.
            await Task.Delay(50);
            var bytes = selection.IsWindow
                ? await TranscribeScreenshotSource.CaptureWindowAsync(selection.Window)
                : await TranscribeScreenshotSource.CaptureRectangleAsync(selection.Area, startingMonitor);
            dispatch(() =>
            {
                if (disposed) return;
                session.AddCapture(bytes, selection.Title);
                Refresh("Captured");
                overlay.Show(selection.IsWindow ? Native.WindowBounds(selection.Window) : selection.Area);
            });
            await Task.Delay(120);
            dispatch(() => { if (!disposed) { overlay.Hide(); busy = false; Refresh(); } });
        }
        catch (Exception error)
        {
            dispatch(() => { if (!disposed) { overlay.Hide(); busy = false; Refresh("Capture failed; previous captures retained. " + error.Message); } });
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        input.Dispose(); overlay.Dispose(); window.CloseAfterFinish(); disposed = true;
        exporter.Dispose();
    }
}
