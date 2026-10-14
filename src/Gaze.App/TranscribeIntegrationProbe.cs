using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Gaze.Fluent;
using Gaze.Core;

namespace Gaze.App;

// Opt-in native acceptance harness. Own fixture process, folder route, no clipboard
// publication or real settings writes. Restores the pointer and closes its fixture.
internal static class TranscribeIntegrationProbe
{
    private sealed record Fixture(long Handle, int X, int Y, int Width, int Height);
    internal static void RunTarget(string directory)
    {
        using var target = new Form { Text = "Transcribe native acceptance target", BackColor = Color.Lime, TopMost = true, KeyPreview = true, Bounds = new Rectangle(Screen.PrimaryScreen!.WorkingArea.Left + 20, Screen.PrimaryScreen.WorkingArea.Top + 40, 300, 230), StartPosition = FormStartPosition.Manual };
        var text = new Label { Text = "TRANSCRIBE NATIVE FIXTURE", AutoSize = true, Left = 15, Top = 20, BackColor = Color.White };
        target.Controls.Add(text);
        target.MouseDown += (_, e) => File.WriteAllText(Path.Combine(directory, e.Button == MouseButtons.Middle ? "middle-received.txt" : "ordinary-click.txt"), e.Button.ToString());
        target.MouseWheel += (_, _) => File.WriteAllText(Path.Combine(directory, "wheel-received.txt"), "wheel");
        target.MouseDoubleClick += (_, _) => File.WriteAllText(Path.Combine(directory, "double-click.txt"), "double click");
        target.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left) File.WriteAllText(Path.Combine(directory, "left-drag.txt"), "left drag"); };
        target.KeyDown += (_, e) => { if (e.KeyCode == Keys.A) File.WriteAllText(Path.Combine(directory, "key-received.txt"), "A"); };
        target.Shown += (_, _) =>
        {
            var bounds = Native.WindowBounds(target.Handle);
            File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(new Fixture(target.Handle.ToInt64(), bounds.X, bounds.Y, bounds.Width, bounds.Height)));
        };
        Application.Run(target);
    }
    internal static void Run(string evidence)
    {
        evidence = Path.GetFullPath(evidence); Directory.CreateDirectory(evidence);
        var log = new List<string> { $"OS: {Environment.OSVersion}; runtime: {Environment.Version}; monitors: {Screen.AllScreens.Length}" };
        var folder = Path.Combine(evidence, "exports");
        using var messages = new Control(); _ = messages.Handle;
        using var target = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { ArgumentList = { "--transcribe-native-target", evidence }, UseShellExecute = false })!;
        TranscribeController? controller = null;
        TranscribeExporter? exporter = null;
        var pointer = Cursor.Position;
        void Dispatch(Action action) => messages.BeginInvoke(action);
        async Task Wait(Func<bool> ready, string name)
        {
            var clock = Stopwatch.StartNew();
            while (!ready()) { if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new Exception("Timeout: " + name); await Task.Delay(50); }
        }
        bool ChooseClose(TranscribeController value, string label)
        {
            bool chosen = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Finish this Transcribe session?");
                if (dialog == null) return;
                timer.Stop();
                var button = dialog.Controls.OfType<Button>().Single(item => item.Text == label);
                chosen = true; button.PerformClick();
            };
            timer.Start();
            var result = value.RequestClose();
            if (!chosen) throw new Exception("Expected native close prompt was not shown");
            return result;
        }
        async void Check()
        {
            try
            {
                await Wait(() => File.Exists(Path.Combine(evidence, "fixture.json")), "target startup");
                var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(evidence, "fixture.json")))!;
                controller = new TranscribeController(false, folder, Dispatch, (result, exported) => { exporter = result; log.Add("Session completed, exported=" + exported); });
                await Task.Delay(500);
                controller.RenderEvidence(Path.Combine(evidence, "empty-window.png"));
                var inside = new Point(fixture.X + 100, fixture.Y + 120);
                if (!Native.SetWindowPos((nint)fixture.Handle, -1, 0, 0, 0, 0, 0x13))
                    throw new Exception("Could not bring fixture to topmost order");
                SetForegroundWindow((nint)fixture.Handle);
                SetCursorPos(inside.X, inside.Y); await Task.Delay(100);
                if (Native.GetAncestor(Native.WindowFromPoint(inside), 2) != (nint)fixture.Handle)
                    throw new Exception("Native fixture is covered at the injected click point; no input claim can be made");
                SetCursorPos(inside.X, inside.Y);
                log.Add($"Injection pointer: requested={inside}, observed={Cursor.Position}, foregroundFixture={SetForegroundWindow((nint)fixture.Handle)}");
                mouse_event(0x2, 0, 0, 0, 0); mouse_event(0x4, 0, 0, 0, 0);
                await Task.Delay(150);
                if (!File.Exists(Path.Combine(evidence, "ordinary-click.txt")))
                {
                    SetCursorPos(inside.X, inside.Y);
                    mouse_event(0x2, 0, 0, 0, 0); mouse_event(0x4, 0, 0, 0, 0);
                    log.Add("Retried ordinary click after foreground activation");
                }
                await Wait(() => File.Exists(Path.Combine(evidence, "ordinary-click.txt")), "ordinary left click");
                mouse_event(0x800, 0, 0, 120, 0);
                await Wait(() => File.Exists(Path.Combine(evidence, "wheel-received.txt")), "ordinary wheel");
                log.Add("PASS ordinary left-click and wheel reach target during capture session");
                for (int i = 0; i < 2; i++) { mouse_event(0x2, 0, 0, 0, 0); mouse_event(0x4, 0, 0, 0, 0); await Task.Delay(80); }
                await Wait(() => File.Exists(Path.Combine(evidence, "double-click.txt")), "ordinary double click");
                mouse_event(0x2, 0, 0, 0, 0); await Task.Delay(80);
                SetCursorPos(inside.X + 15, inside.Y + 15); await Task.Delay(100); mouse_event(0x4, 0, 0, 0, 0);
                await Wait(() => File.Exists(Path.Combine(evidence, "left-drag.txt")), "ordinary left drag");
                if (Native.GetForegroundWindow() != (nint)fixture.Handle) throw new Exception("Fixture lost keyboard focus; refusing key injection");
                keybd_event(0x41, 0, 0, 0); keybd_event(0x41, 0, 2, 0);
                await Wait(() => File.Exists(Path.Combine(evidence, "key-received.txt")), "ordinary keyboard");
                log.Add("PASS ordinary double-click, left-drag and keyboard reach target during capture session");
                SetCursorPos(inside.X, inside.Y);
                mouse_event(0x20, 0, 0, 0, 0); await Task.Delay(80); mouse_event(0x40, 0, 0, 0, 0);
                await Wait(() => controller.Count == 1 && !controller.IsBusy, "window capture");
                log.Add("PASS actual native middle-click produces one retained window capture");
                if (File.Exists(Path.Combine(evidence, "middle-received.txt"))) throw new Exception("Capture middle-button leaked to target");
                SetCursorPos(inside.X, inside.Y); mouse_event(0x20, 0, 0, 0, 0); await Task.Delay(80);
                SetCursorPos(inside.X + 70, inside.Y + 45); await Task.Delay(100); mouse_event(0x40, 0, 0, 0, 0);
                await Wait(() => controller.Count == 2 && !controller.IsBusy, "rectangle capture");
                log.Add("PASS actual native middle-drag produces a second retained rectangle");
                using (var rectangleBytes = new MemoryStream(controller.Captures[1].GetPngBytes()))
                using (var rectangle = new Bitmap(rectangleBytes))
                {
                    File.WriteAllBytes(Path.Combine(evidence, "rectangle.png"), controller.Captures[1].GetPngBytes());
                    var pixel = rectangle.GetPixel(rectangle.Width / 2, rectangle.Height / 2);
                    log.Add($"Rectangle observed: {rectangle.Width}x{rectangle.Height}, center={pixel}");
                    if (rectangle.Width != 70 || rectangle.Height != 45 || pixel.G < 200 || pixel.R > 40 || pixel.B > 40)
                        throw new Exception("Rectangle dimensions/pixels do not match selected fixture area");
                }
                log.Add("PASS rectangle PNG decodes to selected 70x45 pixels without red selection/feedback at center");
                controller.RenderEvidence(Path.Combine(evidence, "two-captures.png"));
                var retainedPng = controller.Captures[0].GetPngBytes();
                controller.UndoForValidation();
                if (controller.Count != 1) throw new Exception("Undo did not remove latest capture");
                log.Add("PASS Undo retains first window capture");
                controller.Finish();
                await Wait(() => exporter != null, "Finish export");
                var zipPath = exporter!.SavedPath ?? throw new Exception("No saved ZIP");
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var png = zip.Entries.Single(entry => entry.Name.EndsWith(".png"));
                    using var stream = png.Open(); using var image = new Bitmap(stream);
                    var pixel = image.GetPixel(image.Width / 2, image.Height / 2);
                    if (pixel.G < 200 || pixel.R > 40 || pixel.B > 40) throw new Exception("Exported window pixels do not match fixture");
                    if (zip.GetEntry("instructions.txt") == null) throw new Exception("Missing instructions");
                }
                if (Directory.GetFiles(folder, "*.mp4").Length != 0) throw new Exception("Unexpected MP4");
                log.Add("PASS Finish closes session, produces ordered ZIP with decodable fixture PNG and instructions, no MP4");
                SetCursorPos(inside.X, inside.Y); mouse_event(0x20, 0, 0, 0, 0); mouse_event(0x40, 0, 0, 0, 0);
                await Wait(() => File.Exists(Path.Combine(evidence, "middle-received.txt")), "middle-button after teardown");
                log.Add("PASS middle-button consumed during session and restored to target after Finish");
                exporter.Dispose(); exporter = null; controller = null;
                bool reentrantCompletion = false, callbackActive = false;
                controller = new TranscribeController(false, folder, Dispatch, (result, _) =>
                {
                    if (callbackActive) reentrantCompletion = true;
                    result.Dispose();
                }, captures =>
                {
                    callbackActive = true;
                    controller!.Cancel();
                    if (reentrantCompletion || controller.Count != 1) throw new Exception("Cancel closed or discarded while export was active");
                    if (controller.RequestClose()) throw new Exception("Close accepted while export was active");
                    controller.Finish();
                    callbackActive = false;
                });
                SetCursorPos(inside.X, inside.Y); mouse_event(0x20, 0, 0, 0, 0); mouse_event(0x40, 0, 0, 0, 0);
                await Wait(() => controller.Count == 1 && !controller.IsBusy, "reentry fixture capture");
                controller.Finish();
                if (reentrantCompletion || callbackActive) throw new Exception("Export reentry guard failed");
                controller = null;
                log.Add("PASS injected export callback cannot Cancel, close or reenter Finish during export");
                var retained = new TranscribeSession(); retained.AddCapture(retainedPng, "close fixture");
                int exportAttempts = 0; bool closeCompleted = false;
                controller = new TranscribeController(false, folder, Dispatch, (result, _) => { result.Dispose(); closeCompleted = true; }, _ =>
                {
                    if (++exportAttempts == 1) throw new IOException("Injected export failure");
                }, retained);
                if (ChooseClose(controller, "Keep capturing") || controller.Count != 1 || closeCompleted) throw new Exception("Keep capturing closed/discarded batch");
                if (ChooseClose(controller, "Finish") || closeCompleted || controller.Count != 1 || retained.State != TranscribeState.ExportFailed)
                    throw new Exception("Failed Finish allowed close or lost batch");
                controller.RenderEvidence(Path.Combine(evidence, "retry-window.png"));
                controller.Finish();
                if (!closeCompleted || exportAttempts != 2) throw new Exception("Retry did not finish retained batch");
                controller = null;
                log.Add("PASS real close dialog Keep capturing and failed Finish retain batch; Retry finishes and closes");
                var discardBatch = new TranscribeSession(); discardBatch.AddCapture(retainedPng, "discard fixture");
                bool discarded = false;
                controller = new TranscribeController(false, folder, Dispatch, (result, exported) =>
                { if (exported) throw new Exception("Discard marked exported"); result.Dispose(); discarded = true; }, _ => throw new Exception("Discard exported"), discardBatch);
                if (!ChooseClose(controller, "Discard") || !discarded || discardBatch.Count != 0) throw new Exception("Discard did not close/release batch");
                controller = null;
                log.Add("PASS real close dialog Discard clears batch and closes without export");
                for (int i = 0; i < 2; i++)
                {
                    bool finished = false;
                    controller = new TranscribeController(false, folder, Dispatch, (result, _) => { result.Dispose(); finished = true; });
                    controller.Finish();
                    if (!finished) throw new Exception("Empty session did not close");
                    controller = null;
                }
                if (Directory.GetFiles(folder, "*.zip").Length != 1) throw new Exception("Empty session exported a ZIP");
                log.Add("PASS repeated start/empty Finish releases hooks and creates no extra ZIP");
                log.Add("UNTESTED: multi-monitor/mixed DPI; tray-host quit routing, clipboard integrated export, video regression");
                Environment.ExitCode = 0;
            }
            catch (Exception error) { Environment.ExitCode = 1; log.Add("FAIL: " + error); }
            finally
            {
                try { controller?.Dispose(); exporter?.Dispose(); }
                catch (Exception error) { log.Add("Cleanup failure: " + error); Environment.ExitCode = 1; }
                SetCursorPos(pointer.X, pointer.Y);
                target.Refresh(); if (!target.HasExited) target.CloseMainWindow();
                File.WriteAllLines(Path.Combine(evidence, "result.txt"), log);
                PanelWindow.StopHostDispatcher();
            }
        }
        PanelWindow.QueueOnHost(Check); PanelWindow.RunHostDispatcher();
    }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
}
