using Gaze.Core;

internal static class TranscribeBatchChecks
{
    internal static void Run(Action<string, Action> test)
    {
        foreach (bool claude in new[] { true, false })
        {
            int countLimit = claude ? 20 : 100;
            long byteLimit = claude ? 29_000_000 : 268_435_456;
            test($"Batch exact count boundary and rejection preserve order (Claude={claude})", () =>
            {
                var s = new TranscribeSession(claude);
                for (int i = 0; i < countLimit; i++) Check(s.AddCapture(new byte[] { (byte)i }, $"image {i}"), "Exact count must fit");
                var before = s.Captures.ToArray();
                Check(!s.AddCapture(new byte[] { 99 }, "rejected"), "One over count must reject");
                Check(s.LastCaptureRejection.ToString() == "CountLimit", "Count rejection reason");
                Check(s.Captures.SequenceEqual(before) && s.TotalPngBytes == countLimit, "Rejection preserves objects, order and bytes");
                Check(s.Undo() && s.TotalPngBytes == countLimit - 1, "Undo recovers count and bytes");
                Check(s.AddCapture(new byte[] { 42 }, "replacement") && s.Latest.Sequence == countLimit + 1, "Rejected capture consumes no sequence");
                Check(s.LastCaptureRejection.ToString() == "None", "Success clears rejection");
                Check(!s.AddCapture(new byte[] { 99 }, "full") && s.NextBatch(), "Reset rejected full batch");
                Check(s.Count == 0 && s.TotalPngBytes == 0 && s.LastCaptureRejection == TranscribeCaptureRejection.None, "Reset recovers capacity and clears warning");
            });
            test($"Batch exact PNG byte boundary and Undo recovery (Claude={claude})", () =>
            {
                var s = new TranscribeSession(claude);
                Check(s.Limits.MaxCount == countLimit && s.Limits.MaxPngBytes == byteLimit, "Selected preset");
                Check(s.AddCapture(new byte[(int)byteLimit - 1], "large"), "One below fits");
                Check(s.AddCapture(new byte[] { 7 }, "boundary") && s.TotalPngBytes == byteLimit, "Exact aggregate bytes fit");
                var before = s.Captures.ToArray();
                Check(!s.AddCapture(new byte[] { 8 }, "overflow"), "One byte above rejects");
                Check(s.LastCaptureRejection.ToString() == "ByteLimit" && s.TotalPngBytes == byteLimit, "Byte reason and accounting");
                Check(s.Captures.SequenceEqual(before) && s.Latest.GetPngBytes()[0] == 7, "Rejection preserves ordered immutable captures");
                Check(s.Undo() && s.TotalPngBytes == byteLimit - 1, "Undo subtracts removed bytes");
                Check(s.AddCapture(new byte[] { 9 }, "recovered"), "Freed bytes reusable");
            });
        }
        test("Next Batch clears current contents and warning while preserving capture lifecycle", () =>
        {
            var s = new TranscribeSession();
            s.AddCapture(new byte[] { 1, 2 }, "old");
            Check(s.NextBatch(), "Capturing can start next batch");
            Check(s.Count == 0 && s.TotalPngBytes == 0 && s.Latest == null && s.State == TranscribeState.Capturing, "Next batch resets count, bytes and thumbnail");
            Check(s.LastCaptureRejection.ToString() == "None", "Reset clears warning");
            Check(!s.Undo() && s.NextBatch(), "Empty Undo and repeated reset harmless");
            Check(s.AddCapture(new byte[] { 3 }, "new") && s.TotalPngBytes == 1, "New batch accepts capture");
            Check(!s.Finish((Action<IReadOnlyList<TranscribeCapture>>)(_ => throw new IOException("busy"))), "Export failure");
            Check(!s.NextBatch() && s.TotalPngBytes == 1, "Legacy failed export retains retry batch");
            Check(s.Finish((Action<IReadOnlyList<TranscribeCapture>>)(_ => { })) && s.TotalPngBytes == 0, "Finish clears accounting");
            Check(!s.NextBatch(), "Closed session cannot restart");
            var discard = new TranscribeSession(); discard.AddCapture(new byte[] { 1 }, "discard");
            discard.RequestClose(TranscribeCloseChoice.Discard, (Action<IReadOnlyList<TranscribeCapture>>)(_ => { }));
            Check(discard.TotalPngBytes == 0, "Discard clears accounting");
        });
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
