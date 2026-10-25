using Gaze.Core;
using Gaze.Fluent;
namespace Gaze.App;

// Real adapter routing and controller, with only native acquisition/publication replaced.
internal static class TranscribeTriggerProbe
{
    internal static void Run(string evidence)
    {
        Directory.CreateDirectory(evidence);
        var log = new List<string>();
        // Negative tests must fail with logs, never a Windows crash dialog.
        SetErrorMode(GetErrorMode() | 2u);
        WerGetFlags(new nint(-1), out var reportingFlags);
        WerSetFlags(reportingFlags | 32u);
        bool dispatcherFault = false;
        PanelWindow.QueueOnHost(() => { });
        var app = System.Windows.Application.Current;
        void RecordFault(Exception error)
        {
            dispatcherFault = true; Environment.ExitCode = 1;
            File.AppendAllText(Path.Combine(evidence, "unhandled.txt"), error + Environment.NewLine);
        }
        app.DispatcherUnhandledException += (_, args) =>
        {
            RecordFault(args.Exception); args.Handled = true;
            app.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            RecordFault(args.Exception);
            app.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            File.AppendAllText(Path.Combine(evidence, "unhandled.txt"), args.ExceptionObject + Environment.NewLine);
        async void RunChecks()
        {
            TranscribeController? controller = null;
            var exporters = new List<TranscribeExporter>();
            try
            {
                using var image = new Bitmap(10, 10);
                using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                byte[] png = stream.ToArray();
                var queued = new List<Action>();
                TranscribeInput input = null!;
                int requests = 0, publications = 0;
                var pending = new TaskCompletionSource<byte[]>();
                var selections = new List<TranscribeSelection>();
                void Assert(bool value, string message) { if (!value) throw new Exception(message); log.Add("PASS " + message); }
                void Flush() { var work = queued.ToArray(); queued.Clear(); foreach (var action in work) action(); }
                void Key(int key, bool down) => input.RouteKey(key, down, false, new(-50,-50));
                void New()
                {
                    controller = new(false, evidence, action => queued.Add(action), (exporter, _) => exporters.Add(exporter),
                        export: _ => { }, publish: _ => publications++,
                        captureSource: (selection, _) => { requests++; selections.Add(selection); return pending.Task; },
                        trigger: TranscribeTrigger.PrintScreen,
                        inputFactory: queue => input = new(queue, TranscribeTrigger.PrintScreen, installHooks: false,
                            snapshot: point => new(0, point, controller!.WindowForValidation.NativeHandle, "fixture", new(-100,-100,100,100))));
                }
                async Task WaitFor(Func<bool> condition)
                {
                    for (int i = 0; i < 200 && !condition(); i++) { await Task.Delay(10); Flush(); }
                    Assert(condition(), "controlled capture reached expected state");
                }
                // Closing before the queued placement runs must be safe.
                New(); controller!.Dispose(); controller = null; await Task.Delay(50);
                Assert(!dispatcherFault, "immediate close cannot run deferred placement on closed window");
                // Stale down cannot create a gesture completed by a current unmatched up.
                New(); await Task.Delay(30); Key(44,true); controller!.UndoForValidation(); Flush(); Key(44,false); Flush();
                await Task.Delay(80); Assert(requests == 0 && !controller.HasGestureForValidation, "queued stale down cannot resurrect gesture");
                controller.Dispose();
                // Queue old move/up, cancel, start a new gesture, then deliver old events.
                publications = 0; New(); await Task.Delay(30); Key(44,true); Flush(); input.RouteMouse(0x200,new(-10,-10)); Key(44,false);
                var stale = queued.ToArray(); queued.Clear(); controller!.UndoForValidation();
                Key(44,true); Flush(); foreach (var action in stale) action();
                Assert(!controller.IsBusy, "queued stale up cannot complete current gesture");
                Key(44,false); Flush(); await WaitFor(() => requests == 1);
                Assert(selections.Single().IsWindow, "queued stale move cannot turn current tap into drag");
                pending.SetResult(png); await Task.Delay(220); Flush();
                Assert(controller.Count == 1 && publications == 2, "current adapter tap captures and publishes exactly once after empty Undo");
                controller.Dispose();
                foreach (string operation in new[] { "Undo", "NextBatch", "Escape", "Close" })
                {
                    requests = 0; publications = 0; pending = new(); New(); await Task.Delay(30);
                    Key(44,true); Flush(); Key(44,false); Flush(); await WaitFor(() => requests == 1);
                    switch (operation)
                    {
                        case "Undo": controller!.UndoForValidation(); break;
                        case "NextBatch": controller!.NextBatchForValidation(); break;
                        case "Escape": Key(44,true); Key(27,true); Flush(); Key(27,false);
                            var release = input.RouteKey(44,false,false,new(-50,-50));
                            Assert(release.Consume && release.Message == 0, "Escape owns release without completing capture"); Flush(); break;
                        case "Close": controller!.RequestClose(); break;
                    }
                    int before = publications;
                    pending.SetResult(png); await Task.Delay(90); Flush();
                    Assert(controller!.Count == 0 && publications == before, operation + " rejects asynchronous canceled result");
                    if (operation != "Close")
                    {
                        pending = new(); Key(44,true); Flush(); Key(44,false); Flush();
                        await WaitFor(() => requests == 2);
                        await Task.Delay(160); Flush();
                        Assert(controller.IsBusy, operation + " stale completion cannot clear newer capture busy state");
                    }
                    controller.Dispose(); controller = null;
                }
                Environment.ExitCode = dispatcherFault ? 1 : 0;
            }
            catch (Exception error) { log.Add("FAIL " + error); Environment.ExitCode = 1; }
            finally
            {
                controller?.Dispose(); foreach (var exporter in exporters) exporter.Dispose();
                File.WriteAllLines(Path.Combine(evidence,"result.txt"),log);
                await Task.Delay(250); // Allow in-flight async capture continuations to leave the UI dispatcher.
                PanelWindow.StopHostDispatcher();
            }
        }
        PanelWindow.QueueOnHost(RunChecks); PanelWindow.RunHostDispatcher();
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint GetErrorMode();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int WerGetFlags(nint process, out uint flags);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);
}
