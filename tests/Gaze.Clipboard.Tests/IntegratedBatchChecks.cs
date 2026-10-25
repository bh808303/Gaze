using Gaze.App;
using Gaze.Core;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
internal static class IntegratedBatchChecks
{
    internal static void Run()
    {
        foreach (var change in new[] { "retry", "capture", "undo", "reset" })
        {
            bool failZip = true; int pngPublications = 0, zipPublications = 0, closed = 0;
            var retryFlow = new TranscribeBatchFlow(new TranscribeSession(true), _ => pngPublications++,
                _ => { zipPublications++; if (failZip) throw new IOException("ZIP unavailable"); }, _ => closed++);
            retryFlow.Accept([1], "first", retryFlow.Generation); retryFlow.Zip();
            Check(retryFlow.NeedsRetry && !retryFlow.Closed && closed == 0, "ZIP failure retains batch and panel");
            retryFlow.Retry();
            Check(zipPublications == 2 && closed == 0 && retryFlow.NeedsRetry && retryFlow.Session.Count == 1,
                "repeated failed ZIP Retry keeps captures and panel");
            failZip = false;
            if (change == "capture") retryFlow.Accept([2], "new", retryFlow.Generation);
            if (change == "undo") retryFlow.Undo();
            if (change == "reset") retryFlow.NextBatch();
            retryFlow.Retry();
            Check(change == "retry" ? zipPublications == 3 && closed == 1 && retryFlow.Closed && pngPublications == 1
                : zipPublications == 2 && closed == 0 && !retryFlow.Closed && pngPublications == 3,
                "ZIP Retry must retry archive and close only on success; edits supersede ZIP intent: " + change);
        }
        var byteFlow = new TranscribeBatchFlow(new TranscribeSession(true), _ => {}, _ => {}, _ => {});
        byteFlow.Accept(new byte[29_000_000], "at size limit", byteFlow.Generation);
        Check(!byteFlow.Accept([1], "over size", byteFlow.Generation) && byteFlow.Session.Count == 1
            && byteFlow.Status.Contains("29 MB") && byteFlow.Status.Contains("Next Batch or ZIP") && !byteFlow.Status.Contains("ByteLimit"), "byte rejection shows actual limit and recovery guidance");
        var standardFlow = new TranscribeBatchFlow(new TranscribeSession(false), _ => {}, _ => {}, _ => {});
        for (int i = 0; i < 100; i++) standardFlow.Accept([1], "standard", standardFlow.Generation);
        Check(!standardFlow.Accept([1], "over count", standardFlow.Generation) && standardFlow.Status.Contains("100 images / 256 MiB"), "off setting warning names standard limits");
        var root = Path.GetFullPath(Path.Combine("artifacts", "integrated-batches", Guid.NewGuid().ToString("N")));
        OleDataObject? clipboard = (OleDataObject)new System.Windows.Forms.DataObject("previous");
        var previous = clipboard;
        bool fail = false, failWrite = false; int writes = 0, closes = 0; bool zipped = false;
        using var publisher = new TranscribeBatchPublisher(root,
            data => { if (fail) return unchecked((int)0x800401D0); clipboard = data; return 0; },
            data => ReferenceEquals(data, clipboard) ? 0 : 1, () => { clipboard = null; return 0; },
            (path, bytes) => { writes++; File.WriteAllBytes(path, bytes); if (failWrite) throw new IOException("partial"); });
        TranscribeBatchFlow New(bool limited = true) => new(new TranscribeSession(limited), publisher.Publish,
            captures => publisher.PublishZip(captures, limited), value => { closes++; zipped = value; });
        string[] Paths() => ((System.Windows.Forms.DataObject)clipboard!).GetFileDropList().Cast<string>().ToArray();
        var flow = New();
        Check(ReferenceEquals(previous, clipboard) && !flow.CanZip, "empty Start preserves prior clipboard; ZIP disabled");
        flow.Zip(); Check(closes == 0 && ReferenceEquals(previous, clipboard), "empty ZIP cannot close or mutate clipboard");
        flow.Accept([1,2], "first", flow.Generation);
        Check(!ReferenceEquals(previous, clipboard), "accepted capture must publish live batch");
        var first = Paths().Single();
        flow.Accept([3], "second", flow.Generation);
        Check(Paths().Length == 2 && Paths()[0] == first && writes == 2, "full batch reuses immutable capture staging");
        fail = true; flow.Accept([4], "third", flow.Generation);
        Check(flow.Session.Count == 3 && Paths().Length == 2 && flow.NeedsRetry, "failed copy retains new capture and old offering");
        var warning = flow.Status; Check(warning.Length > 0, "copy error persistent state");
        fail = false; flow.Undo(); Check(Paths().Length == 2 && writes == 3 && !flow.NeedsRetry, "Undo publishes latest snapshot without rewriting");
        failWrite = true; flow.Accept([5], "partial", flow.Generation);
        Check(flow.NeedsRetry && Paths().Length == 2 && writes == 4, "failed write preserves prior clipboard");
        failWrite = false; flow.Retry();
        Check(Paths().Length == 3 && writes == 5 && !flow.NeedsRetry, "retry writes only failed capture");
        flow.Retry(); Check(writes == 5, "retry reuses all successful staging");
        var old = Paths(); int stale = flow.Generation;
        flow.NextBatch(); Check(flow.Session.Count == 0 && flow.Session.TotalPngBytes == 0 && flow.Session.Latest == null && clipboard == null, "Next Batch resets and clears owned offering");
        Check(!flow.Accept([9], "late", stale) && flow.Session.Count == 0, "pending capture cannot reappear after reset");
        flow.Accept([8], "close", flow.Generation); var offered = clipboard;
        flow.Close(); Check(closes == 1 && !zipped && ReferenceEquals(offered, clipboard) && old.All(File.Exists), "X/Stop closes without ZIP preserving files/offering");
        Check(!flow.Accept([9], "late", flow.Generation), "pending capture cannot reappear after close");
        flow = New(); Check(ReferenceEquals(offered, clipboard), "new session preserves prior offering");
        flow.Accept([7], "new identity", flow.Generation);
        Check(File.ReadAllBytes(Paths().Single()).SequenceEqual(new byte[]{7}), "new session sequence one cannot reuse prior capture identity");
        fail = true; flow.Zip(); Check(closes == 1 && flow.CanZip && flow.NeedsRetry, "failed ZIP retains open batch");
        flow.NextBatch(); fail = false; flow.Retry(); Check(clipboard == null && closes == 1, "retry after reset cannot publish stale ZIP");
        flow.Accept([6], "current ZIP", flow.Generation); flow.Zip();
        Check(closes == 2 && zipped && Paths().Single().EndsWith(".zip"), "only successful current ZIP closes");
        using (var archive = System.IO.Compression.ZipFile.OpenRead(Paths().Single()))
            Check(archive.Entries.Count == 2, "current ZIP plus instructions");
        flow = New(); for (int i=0;i<20;i++) flow.Accept([1], "limit", flow.Generation);
        offered = clipboard; int countWrites = writes;
        Check(!flow.Accept([2], "rejected", flow.Generation) && ReferenceEquals(offered, clipboard) && writes == countWrites, "rejected capture preserves offering");
        Check(flow.Status.Contains("20") && flow.Status.Contains("Next Batch") && flow.Status.Contains("ZIP") && !flow.Status.Contains("CountLimit"), "limit warning remains in coordinator state for refresh");
        flow.Undo(); flow.NextBatch();
        clipboard = previous; flow.NextBatch(); Check(ReferenceEquals(previous, clipboard), "reset never clears unrelated clipboard");
        Console.WriteLine("PASS integrated batch lifecycle, failures, identity cache, failed-write retry, limits and close versus ZIP");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
