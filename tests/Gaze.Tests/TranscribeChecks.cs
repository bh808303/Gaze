using Gaze.Core;

internal static class TranscribeChecks
{
    internal static void Run(Action<string, Action> test)
    {
        test("Transcribe gestures distinguish click threshold and clip reversed drags", () =>
        {
            var monitor = new System.Drawing.Rectangle(-100, -100, 100, 100);
            TranscribeGesture Gesture() => new(new(-50, -50), new(4, 4), 42, "start title", monitor);
            var click = Gesture().Complete(new(-49, -49));
            Check(click is { IsWindow: true, Window: 42, Title: "start title" }, "Inside drag threshold clicks starting window");
            var boundary = Gesture().Complete(new(-48, -50));
            Check(boundary == null, "Outside half-open threshold is a zero-height drag, not a click");
            var reversed = Gesture().Complete(new(-150, -150));
            Check(reversed is { IsWindow: false, Title: "start title" } && reversed.Area == new System.Drawing.Rectangle(-100, -100, 50, 50), "Reversed drag clipped to starting monitor");
            var across = Gesture().Complete(new(100, 100));
            Check(across!.Area == new System.Drawing.Rectangle(-50, -50, 50, 50), "Cross-monitor selection clipped");
            var cancelled = Gesture(); cancelled.Cancel();
            Check(cancelled.Complete(new(-49, -49)) == null && cancelled.Preview(new(-10, -10)).IsEmpty, "Cancelled gesture adds nothing");
            var once = Gesture(); once.Complete(new(-49, -49));
            Check(once.Complete(new(-49, -49)) == null, "Repeated release cannot capture twice");
            var back = Gesture(); back.Preview(new(-90, -90));
            Check(back.Complete(new(-49, -49)) is { IsWindow: false }, "Drag remains a drag after returning inside threshold");
            Check(new TranscribeGesture(new(-50, -50), new(4, 4), 0, "desktop", monitor).Complete(new(-50, -50)) == null, "Missing window cannot click capture");
        });
        test("Transcribe retains immutable captures and Undo preserves sequence", () =>
        {
            var s = new TranscribeSession();
            Check(!s.Undo() && !s.AddCapture([], "invalid"), "Empty input must be harmless");
            byte[] bytes = [1, 2, 3];
            s.AddCapture(bytes, "first"); bytes[0] = 99;
            var copy = s.Captures[0].GetPngBytes(); copy[0] = 88;
            Check(s.Captures[0].GetPngBytes()[0] == 1, "Capture bytes must be owned");
            s.AddCapture([4], "second"); s.Undo(); s.AddCapture([5], "third");
            Check(s.Count == 2 && s.Captures[1].Sequence == 3 && s.Latest!.Title == "third", "Undo only removes latest; sequences never repeat");
        });
        test("Transcribe empty finish and discard never export", () =>
        {
            var s = new TranscribeSession();
            Check(s.Finish(_ => throw new Exception("Unexpected export")) && s.State == TranscribeState.Closed, "Empty finish closes");
            var discard = new TranscribeSession(); discard.AddCapture([1], "x");
            Check(!discard.RequestClose(TranscribeCloseChoice.KeepCapturing, _ => throw new Exception()), "Keep capturing prevents quit");
            Check(discard.Count == 1 && discard.State == TranscribeState.Capturing, "Keep retains batch");
            Check(discard.RequestClose(TranscribeCloseChoice.Discard, _ => throw new Exception()) && discard.Count == 0, "Discard closes without export");
        });
        test("Transcribe failed Finish prevents quit and Retry preserves ordered batch", () =>
        {
            var s = new TranscribeSession(); s.AddCapture([7], "a"); s.AddCapture([8], "b");
            Check(!s.RequestClose(TranscribeCloseChoice.Finish, _ => throw new IOException("clipboard busy")), "Failed finish prevents quit");
            Check(s.State == TranscribeState.ExportFailed && s.Count == 2 && s.LastExportError is IOException, "Failure retains batch and error");
            Check(!s.AddCapture([9], "late") && !s.Undo(), "Failed export freezes retry contents");
            bool guarded = false;
            Check(s.Finish(captures =>
            {
                guarded = !s.Finish(_ => throw new Exception()) && !s.RequestClose(TranscribeCloseChoice.Discard, _ => { });
                Check(captures.Select(c => c.Title).SequenceEqual(new[] { "a", "b" }), "Retry retains order");
                Check(captures[0].GetPngBytes()[0] == 7, "Retry retains bytes");
            }), "Retry succeeds");
            Check(guarded && s.State == TranscribeState.Closed && s.Count == 0 && s.LastExportError == null, "Success releases batch; reentry blocked");
        });
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
