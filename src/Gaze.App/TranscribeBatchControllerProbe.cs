using Gaze.Core;
using Gaze.Fluent;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
namespace Gaze.App;

// Runs real controller/panel/hook lifecycle with injected screenshots and clipboard only.
internal static class TranscribeBatchControllerProbe
{
    internal static void Run(string evidence)
    {
        evidence = Path.GetFullPath(evidence); Directory.CreateDirectory(evidence);
        var log = new List<string>();
        async void Check()
        {
            OleDataObject? clipboard = (OleDataObject)new DataObject("prior");
            var previous = clipboard;
            bool fail = false, failClear = false; int closes = 0, writes = 0; bool exported = false;
            TranscribeController? controller = null;
            var exporters = new List<TranscribeExporter>();
            using var publisher = new TranscribeBatchPublisher(Path.Combine(evidence, "staging"),
                data => { if (fail) return unchecked((int)0x800401D0); clipboard = data; return 0; },
                data => ReferenceEquals(data, clipboard) ? 0 : 1,
                () => { if (failClear) return unchecked((int)0x800401D0); clipboard = null; return 0; },
                (path, bytes) => { writes++; File.WriteAllBytes(path, bytes); });
            using var bitmap = new Bitmap(10, 10);
            using var png = new MemoryStream(); bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = png.ToArray();
            Task<byte[]> source = Task.FromResult(bytes);
            var selection = new TranscribeSelection(false, 0, "fixture", new Rectangle(0,0,10,10));
            string[] Paths() => ((DataObject)clipboard!).GetFileDropList().Cast<string>().ToArray();
            void Assert(bool condition, string message) { if (!condition) throw new Exception(message); log.Add("PASS " + message); }
            TranscribeController New(bool limited = true, Action<IReadOnlyList<TranscribeCapture>>? archive = null) => new(false, evidence, action => action(), (result, zip) =>
                { exporters.Add(result); closes++; exported = zip; }, archive ?? (captures => publisher.PublishZip(captures, limited)),
                claudeDesktopClipboard: limited, publish: publisher.Publish, captureSource: (_, _) => source);
            async Task Capture()
            {
                controller!.CaptureForValidation(selection);
                for (int i=0; controller.IsBusy && i<100; i++) await Task.Delay(10);
                if (controller.IsBusy) throw new Exception("capture timeout");
            }
            try
            {
                controller = New(); var window = controller.WindowForValidation;
                Assert(window.Topmost && !window.ZipEnabled && !window.HasThumbnail && ReferenceEquals(previous, clipboard), "topmost empty Start preserves clipboard; ZIP disabled");
                controller.Finish(); Assert(closes == 0, "empty ZIP stays open");
                await Capture(); var first = Paths().Single();
                Assert(controller.Count == 1 && window.HasThumbnail && window.ZipEnabled && window.BatchSummary.Contains(bytes.Length.ToString("N0") + " bytes"), "accepted capture updates count size thumbnail ZIP and live clipboard");
                await Capture(); Assert(Paths().Length == 2 && Paths()[0] == first && writes == 2, "controller publishes full batch without rewriting staged captures");
                fail = true; await Capture();
                Assert(controller.Count == 3 && Paths().Length == 2 && window.RetryVisible && window.ClipboardStatus.Contains("error"), "failed copy retains capture and persistent clipboard error after async refresh");
                fail = false; controller.UndoForValidation(); Assert(Paths().Length == 2 && writes == 3 && !window.RetryVisible, "Undo republishes current snapshot without rewrites");
                failClear = true; controller.NextBatchForValidation();
                Assert(controller.Count == 0 && !window.HasThumbnail && !window.ZipEnabled && window.RetryVisible, "failed reset clear reports error with empty panel");
                failClear = false; controller.RetryForValidation(); Assert(clipboard == null && !window.RetryVisible, "retry after Next Batch clears owned current offering");
                var pending = new TaskCompletionSource<byte[]>(); source = pending.Task;
                controller.CaptureForValidation(selection); await Task.Delay(80); controller.NextBatchForValidation(); pending.SetResult(bytes); await Task.Delay(220);
                Assert(controller.Count == 0 && !controller.IsBusy && clipboard == null, "capture pending during reset cannot reappear");
                source = Task.FromResult(bytes); await Capture(); var offering = clipboard; var files = Paths();
                pending = new TaskCompletionSource<byte[]>(); source = pending.Task;
                controller.CaptureForValidation(selection); await Task.Delay(80);
                Assert(controller.RequestClose() && closes == 1 && !exported && ReferenceEquals(offering, clipboard), "Stop/X closes during pending capture without export or clipboard mutation");
                pending.SetResult(bytes); await Task.Delay(220);
                Assert(files.All(File.Exists), "close preserves staged files and offering for readers");
                controller = New(); Assert(ReferenceEquals(offering, clipboard), "new session leaves old offering untouched");
                source = Task.FromResult(bytes); await Capture(); fail = true; controller.Finish();
                Assert(closes == 1 && controller.Count == 1 && controller.WindowForValidation.RetryVisible, "ZIP failure keeps current panel batch and retry");
                fail = false; controller.NextBatchForValidation(); controller.RetryForValidation();
                Assert(clipboard == null && closes == 1, "reset after failed ZIP never republishes stale archive");
                await Capture(); controller.Finish();
                Assert(closes == 2 && exported && Paths().Single().EndsWith(".zip"), "successful current ZIP publishes then closes");
                using (var zip = System.IO.Compression.ZipFile.OpenRead(Paths().Single())) Assert(zip.Entries.Count == 2, "current ZIP contains one PNG and instructions");
                controller = New(); for (int i=0;i<20;i++) await Capture(); offering = clipboard;
                await Capture();
                Assert(controller.Count == 20 && ReferenceEquals(offering, clipboard) && controller.WindowForValidation.ClipboardStatus.Contains("20 images / 29 MB") && controller.WindowForValidation.ClipboardStatus.Contains("Next Batch or ZIP") && !controller.WindowForValidation.ClipboardStatus.Contains("CountLimit") && controller.WindowForValidation.BatchSummary.Contains("KB"), "rejected capture preserves old clipboard and warning through refresh");
                controller.RenderEvidence(Path.Combine(evidence,"limit-panel.png"));
                controller.WindowForValidation.Close();
                for (int i=0; closes < 3 && i<100; i++) await Task.Delay(10);
                Assert(closes == 3 && !exported, "actual WPF X/Closing route closes without prompt or ZIP");
                controller = New(archive: captures =>
                {
                    Assert(!controller!.RequestClose(), "reentrant close cannot interrupt ZIP publication");
                    controller.Cancel(); controller.Finish(); controller.NextBatchForValidation();
                    Assert(controller.Count == 1 && closes == 3, "reentrant actions cannot discard/reset or duplicate ZIP completion");
                    publisher.PublishZip(captures, true);
                });
                await Capture(); controller.Finish();
                Assert(closes == 4 && exported, "ZIP completes once after guarded publication");
                controller = New(); await Capture(); fail = true; controller.Finish();
                Assert(closes == 4 && controller.Count == 1 && controller.WindowForValidation.RetryVisible, "failed ZIP exposes Retry without closing");
                controller.RetryForValidation();
                Assert(closes == 4 && controller.Count == 1 && controller.WindowForValidation.RetryVisible, "repeated failed ZIP Retry keeps real panel and batch");
                controller.RenderEvidence(Path.Combine(evidence, "zip-retry-panel.png"));
                fail = false; controller.RetryForValidation();
                Assert(closes == 5 && exported && Paths().Single().EndsWith(".zip"), "real panel Retry after failed ZIP publishes archive then closes");
                controller = null;
                log.Add("UNTESTED: real desktop input/pixels, Claude paste/ingestion, mixed DPI/multiple monitors, native tray shortcuts and video capture.");
                Environment.ExitCode = 0;
            }
            catch (Exception error) { log.Add("FAIL " + error); Environment.ExitCode = 1; }
            finally
            {
                controller?.Dispose(); foreach (var exporter in exporters) exporter.Dispose();
                File.WriteAllLines(Path.Combine(evidence,"result.txt"), log);
                PanelWindow.StopHostDispatcher();
            }
        }
        PanelWindow.QueueOnHost(Check); PanelWindow.RunHostDispatcher();
    }
}
