using Gaze.App;
using Gaze.Core;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
internal static class LiveBatchChecks
{
    internal static void Run()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "live-tests", Guid.NewGuid().ToString("N")));
        OleDataObject? clipboard = null;
        bool fail = false, failWrite = false, failClear = false;
        string[] Paths() => ((System.Windows.Forms.DataObject)clipboard!).GetFileDropList().Cast<string>().ToArray();
        using var publisher = new TranscribeBatchPublisher(root,
            data => { if (fail) return unchecked((int)0x800401D0); clipboard = data; return 0; },
            data => ReferenceEquals(data, clipboard) ? 0 : 1,
            () => { if (failClear) return unchecked((int)0x800401D0); clipboard = null; return 0; },
            (path, bytes) => { File.WriteAllBytes(path, bytes); if (failWrite) throw new IOException("partial write"); });
        var session = new TranscribeSession(false);
        publisher.Publish(session.Captures); Check(clipboard == null, "empty leaves clipboard unchanged");
        session.AddCapture([1,2], "a/b"); session.AddCapture([3,4], "second");
        publisher.Publish(session.Captures);
        Check(clipboard != null, "live capture must publish full PNG file-drop");
        var original = Paths();
        using (var closedPanel = new System.Windows.Forms.Form()) { closedPanel.CreateControl(); }
        session.RequestClose(TranscribeCloseChoice.KeepCapturing, _ => throw new Exception("unexpected export"));
        Check(original.All(File.Exists), "panel closure leaves app-owned backing files available");
        Check(original.Select(Path.GetFileName).SequenceEqual(new[]{"000001_a_b.png", "000002_second.png"}), "ordered sanitized names");
        Check(File.ReadAllBytes(original[1]).SequenceEqual(new byte[]{3,4}), "immutable PNG bytes");
        session.AddCapture([5], "third"); fail = true; Throws(() => publisher.Publish(session.Captures));
        Check(Paths().SequenceEqual(original) && original.All(File.Exists), "publication failure preserves previous data");
        session.Undo(); session.Undo(); fail = false; publisher.Publish(session.Captures);
        Check(Paths().Length == 1, "retry uses latest Undo snapshot");
        failWrite = true; session.AddCapture([6], "partial"); var prior = Paths();
        Throws(() => publisher.Publish(session.Captures));
        Check(Paths().SequenceEqual(prior) && prior.All(File.Exists), "partial write preserves previous offering");
        failWrite = false; session.NextBatch(); failClear = true; Throws(() => publisher.Publish(session.Captures));
        failClear = false; publisher.Publish(session.Captures); Check(clipboard == null, "reset retry clears current owned offering");
        Check(original.All(File.Exists), "previous batches survive reset");
        session.AddCapture([7], "fresh"); publisher.Publish(session.Captures);
        clipboard = (OleDataObject)new System.Windows.Forms.DataObject("unrelated"); var unrelated = clipboard;
        session.NextBatch(); publisher.Publish(session.Captures); Check(ReferenceEquals(clipboard, unrelated), "clear preserves overwritten clipboard");
        session.AddCapture([8], "zip-current"); publisher.PublishZip(session.Captures, true);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(Paths().Single()))
            Check(zip.Entries.Select(e=>e.FullName).SequenceEqual(new[]{"000006_zip-current.png","instructions.txt"}), "ZIP uses only current batch");
        var zipPaths = Paths(); session.NextBatch();
        var noise = new byte[29_000_000]; new Random(42).NextBytes(noise); session.AddCapture(noise, "large");
        Throws(() => publisher.PublishZip(session.Captures, true));
        Check(Paths().SequenceEqual(zipPaths), "ZIP overhead limit checked before clipboard mutation");
                var saved = Path.Combine(root, "saved.zip"); File.WriteAllText(saved, "saved");
        var unknown = Path.Combine(root, "unrelated.txt"); File.WriteAllText(unknown, "untouched");
        Check(TranscribeBatchPublisher.CleanupStale(root).Count > 0 && original.All(File.Exists), "startup refuses active instance lease");
        var stale = Path.Combine(root, "transcribe-" + Guid.NewGuid().ToString("N"));
        var batchId = Guid.NewGuid().ToString("N"); Directory.CreateDirectory(Path.Combine(stale, batchId));
        File.WriteAllText(Path.Combine(stale, "owner"), "Gaze.Transcribe.Live.v1");
        File.WriteAllLines(Path.Combine(stale, "files"), [Path.Combine(batchId, "000001_old.png")]);
        var old = Path.Combine(stale, batchId, "000001_old.png"); File.WriteAllText(old, "old");
        var extra = Path.Combine(stale, "unrelated.zip"); File.WriteAllText(extra, "keep");
        TranscribeBatchPublisher.CleanupStale(root);
        Check(!File.Exists(old) && File.Exists(extra) && File.Exists(saved) && File.Exists(unknown), "stale cleanup removes only journaled artifacts, preserves saved/unrelated files");
        var redirected = Path.Combine(root, "redirected");
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{redirected}\" \"{stale}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true }))
        { process!.WaitForExit(); Check(process.ExitCode == 0, "junction fixture creation"); }
        Throws(() => { using var invalid = new TranscribeBatchPublisher(redirected, _=>0, _=>0, ()=>0); });
        Check(TranscribeBatchPublisher.CleanupStale(redirected).Count > 0 && File.Exists(extra), "cleanup refuses redirected root");
                Directory.Delete(redirected);
        var unsafeInstance = Path.Combine(root, "transcribe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(unsafeInstance);
        File.WriteAllText(Path.Combine(unsafeInstance, "owner"), "Gaze.Transcribe.Live.v1");
        var unsafeBatch = Guid.NewGuid().ToString("N");
        var external = Path.Combine(root, "configured-saved"); Directory.CreateDirectory(external);
        var protectedPng = Path.Combine(external, "saved.png"); File.WriteAllText(protectedPng, "keep saved file");
        File.WriteAllLines(Path.Combine(unsafeInstance, "files"), [Path.Combine(unsafeBatch, "saved.png")]);
        var junction = Path.Combine(unsafeInstance, unsafeBatch);
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{external}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true }))
        { process!.WaitForExit(); Check(process.ExitCode == 0, "nested junction fixture creation"); }
        Check(TranscribeBatchPublisher.CleanupStale(root).Count > 0 && File.Exists(protectedPng), "cleanup never traverses redirected batch to saved files");
        Directory.Delete(junction);
        publisher.Dispose(); Check(original.All(p=>!File.Exists(p)), "app lifetime disposal cleans owned backing files");
        Console.WriteLine("PASS live batch ordering, failure retention, latest retry, ownership, ZIP overhead and lifetime");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected operation failure"); }
}


