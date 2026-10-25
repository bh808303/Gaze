using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Gaze.App;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var clipboardSequence = GetClipboardSequenceNumber();
        try
        {
            // No clipboard publication: consume the real producer via the COM contract.
            using var owner = new TranscribeClipboardOwner();
            owner.Invoke(() =>
            {
                var bytes = new byte[] { 1, 2, 3, 4 };
                OleDataObject data = new VirtualZipDataObject("probe-π.zip", bytes);
                bytes[0] = 99;
                var descriptor = Format("FileGroupDescriptorW", -1, TYMED.TYMED_HGLOBAL);
                var contents = Format("FileContents", 0, TYMED.TYMED_ISTREAM);
                Check(data.QueryGetData(ref descriptor) == 0 && data.QueryGetData(ref contents) == 0, "supported formats");
                var enumerator = data.EnumFormatEtc(DATADIR.DATADIR_GET);
                var formats = new FORMATETC[3]; var fetched = new int[1];
                Check(enumerator.Next(3, formats, fetched) == 1 && fetched[0] == 2, "exactly two formats, no file-drop path");
                enumerator.Reset(); enumerator.Clone(out var clone);
                Check(clone.Next(1, formats, fetched) == 0 && formats[0].cfFormat == descriptor.cfFormat, "enumerator reset/clone");
                data.GetData(ref descriptor, out var medium);
                try
                {
                    var pointer = GlobalLock(medium.unionmember);
                    try
                    {
                        Check(Marshal.ReadInt32(pointer) == 1, "one file descriptor");
                        Check((Marshal.ReadInt32(pointer, 4) & 0x40) != 0, "size flag");
                        Check(Marshal.ReadInt32(pointer, 4 + 64) == 0 && Marshal.ReadInt32(pointer, 4 + 68) == 4, "descriptor byte length");
                        Check(Marshal.PtrToStringUni(pointer + 4 + 72) == "probe-π.zip", "Unicode filename");
                    }
                    finally { GlobalUnlock(medium.unionmember); }
                }
                finally { ReleaseStgMedium(ref medium); }
                var first = Read(data, contents); var second = Read(data, contents);
                Check(first.SequenceEqual(new byte[] { 1, 2, 3, 4 }) && first.SequenceEqual(second), "immutable payload and fresh streams");
                Reject(data, contents with { lindex = 1 }, unchecked((int)0x80040068));
                Reject(data, contents with { tymed = TYMED.TYMED_HGLOBAL }, unchecked((int)0x80040069));
                Reject(data, contents with { dwAspect = DVASPECT.DVASPECT_ICON }, unchecked((int)0x8004006B));
                Reject(data, contents with { ptd = 1 }, unchecked((int)0x80040065));
                Reject(data, descriptor with { lindex = 0 }, unchecked((int)0x80040068));
                var combined = contents with { tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL };
                Check(data.QueryGetData(ref combined) == 0 && Read(data, combined).SequenceEqual(first), "consumer medium bitmask negotiates stream");
                Reject(data, Format("FileDrop", -1, TYMED.TYMED_HGLOBAL), unchecked((int)0x80040064));
                var zip = TranscribeClipboardFixture.Create();
                using var archive = new ZipArchive(new MemoryStream(Read(new VirtualZipDataObject("fixture.zip", zip), contents)));
                Check(archive.Entries.Select(x => x.FullName).SequenceEqual(new[] { "000001_probe.png", "instructions.txt" }), "ZIP entries");
                using var imageStream = archive.Entries[0].Open();
                using var image = new Bitmap(imageStream);
                Check(image.Width == 480 && image.Height == 120, "PNG decodes");
                Check(image.GetPixel(0, 0).ToArgb() == Color.White.ToArgb() &&
                    Enumerable.Range(30, 50).Any(y => Enumerable.Range(12, 400).Any(x => image.GetPixel(x, y).R < 128)), "fixture contains rendered text on white background");
                using var reader = new StreamReader(archive.Entries[1].Open());
                var text = reader.ReadToEnd();
                Check(text.Contains("filename order") && text.Contains("remove duplicates") && text.Contains("without guessing") && !text.Contains("7319"), "instructions do not leak PNG marker");
                Console.WriteLine("PASS isolated IDataObject descriptor, enumeration, immutable/repeated streams, rejected requests, ZIP and PNG decode");
            });
            var caller = Environment.CurrentManagedThreadId;
            var thread = owner.Invoke(() => (Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState()));
            Check(thread.Item1 != caller && thread.Item2 == ApartmentState.STA, "independent STA");
            using (var transientSessionWindow = new Form()) { transientSessionWindow.CreateControl(); }
            Check(owner.Invoke(() => 7319) == 7319, "owner survives session-window disposal and dispatches again");
            // Marshal the real IDataObject CCW across apartments, while its STA pumps.
            var iid = new Guid("0000010e-0000-0000-C000-000000000046");
            var marshaled = owner.Invoke(() =>
            {
                Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(ref iid,
                    new VirtualZipDataObject("cross-apartment.zip", new byte[] { 7, 3, 1, 9 }), out var pointer));
                return pointer;
            });
            Exception? consumerError = null;
            var consumer = new Thread(() =>
            {
                var initialized = false;
                try
                {
                    Marshal.ThrowExceptionForHR(CoInitializeEx(0, 0)); initialized = true;
                    Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(marshaled, ref iid, out var pointer));
                    try
                    {
                        var format = Format("FileContents", 0, TYMED.TYMED_ISTREAM);
                        Check(Method<Query>(pointer, 5)(pointer, ref format) == 0, "marshaled consumer query");
                        Marshal.ThrowExceptionForHR(Method<Get>(pointer, 3)(pointer, ref format, out var medium));
                        Check(ReadMedium(medium).SequenceEqual(new byte[] { 7, 3, 1, 9 }), "marshaled consumer payload");
                        Marshal.ThrowExceptionForHR(Method<Enumerate>(pointer, 8)(pointer, DATADIR.DATADIR_GET, out var enumerator));
                        try
                        {
                            var formats = new FORMATETC[2];
                            Check(Method<Next>(enumerator, 3)(enumerator, 2, formats, out var fetched) == 0 && fetched == 2, "marshaled enumeration");
                        }
                        finally { Marshal.Release(enumerator); }
                    }
                    finally { Marshal.Release(pointer); }
                }
                catch (Exception error) { consumerError = error; }
                finally { if (initialized) CoUninitialize(); }
            }) { IsBackground = true };
            consumer.SetApartmentState(ApartmentState.MTA); consumer.Start();
            Check(consumer.Join(TimeSpan.FromSeconds(15)), "cross-apartment consumer timeout");
            if (consumerError != null) throw new Exception("COM consumer failed", consumerError);
            Console.WriteLine("PASS marshaled MTA consumer retrieves data and enumeration from pumping STA after session-window disposal");
            Console.WriteLine("PASS independent pumping STA survives transient window; no real clipboard mutation");
            var attempts = new List<OleDataObject>();
            var clears = 0;
            using (var failedOwner = new TranscribeClipboardOwner(data =>
            { attempts.Add(data); return attempts.Count == 1 ? unchecked((int)0x800401D0) : 0; }, _ => 1, () => { clears++; return 0; }))
            {
                try { failedOwner.Publish("retry.zip", new byte[] { 7, 3, 1, 9 }); throw new Exception("Failure not surfaced"); }
                catch (COMException error) { Check(error.HResult == unchecked((int)0x800401D0), "publication error preserved"); }
                failedOwner.Retry();
                Check(ReferenceEquals(attempts[0], attempts[1]), "failed payload retained unchanged for retry");
                failedOwner.Invoke(() => Check(Read(attempts[1], Format("FileContents", 0, TYMED.TYMED_ISTREAM)).SequenceEqual(new byte[] { 7, 3, 1, 9 }), "retry payload"));
            }
            Check(clears == 0, "replacement clipboard not cleared");
            using (var currentOwner = new TranscribeClipboardOwner(_ => 0, _ => 0, () => { clears++; return 0; }))
                currentOwner.Publish("owned.zip", new byte[] { 1 });
            Check(clears == 1, "explicit stop clears own offering");
            Console.WriteLine("PASS injected OLE failure retained/retried; stop preserves replacement clipboard and releases own offering");
            owner.Invoke(CheckStaging);
            IntegratedBatchChecks.Run();
            LiveBatchChecks.Run();
            TranscribeExportChecks.Run();
            Check(GetClipboardSequenceNumber() == clipboardSequence, "real clipboard sequence unchanged");
            Console.WriteLine("PASS real clipboard sequence unchanged");
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error); return 1; }
    }

    private static FORMATETC Format(string name, int index, TYMED medium) => new()
    { cfFormat = unchecked((short)RegisterClipboardFormat(name)), dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = medium };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void CheckStaging()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "clipboard-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var unrelated = Path.Combine(root, "unrelated.txt"); File.WriteAllText(unrelated, "keep");
        var bytes = TranscribeClipboardFixture.Create();
        var snapshot = (byte[])bytes.Clone();
        var attempts = new List<OleDataObject>(); int writes = 0, clears = 0;
        bool failPublish = true, failClear = true;
        var offer = new StagedZipOffer(bytes, root, data =>
        { attempts.Add(data); return failPublish ? unchecked((int)0x800401D0) : 0; }, _ => 0,
            () => { clears++; return failClear ? unchecked((int)0x800401D0) : 0; }, (path, payload) =>
            {
                writes++;
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(payload.AsSpan(0, writes == 1 ? 10 : payload.Length));
                if (writes == 1) throw new IOException("injected partial staging failure");
            });
        bytes[0] = 0;
        try { offer.Retry(); throw new Exception("staging error missing"); }
        catch (IOException) { }
        Check(attempts.Count == 0 && !Directory.GetFiles(root, "*.zip", SearchOption.AllDirectories).Any(), "partial stage removed; no publication");
        try { offer.Retry(); throw new Exception("publication error missing"); }
        catch (COMException) { }
        var path = offer.StagedPath!;
        Check(File.ReadAllBytes(path).SequenceEqual(snapshot), "immutable final ZIP retained after publication failure");
        failPublish = false; offer.Retry();
        Check(writes == 2 && ReferenceEquals(attempts[0], attempts[1]) && offer.StagedPath == path, "publication retry reuses staged snapshot and object");
        var drop = new FORMATETC { cfFormat = 15, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        attempts[1].GetData(ref drop, out var medium);
        try
        {
            Check(DragQueryFile(medium.unionmember, uint.MaxValue, null, 0) == 1, "native CF_HDROP contains one file");
            var name = new System.Text.StringBuilder(32768);
            DragQueryFile(medium.unionmember, 0, name, (uint)name.Capacity);
            Check(name.ToString() == path, "native CF_HDROP path matches readable staged ZIP");
        }
        finally { ReleaseStgMedium(ref medium); }
        using (var zip = new ZipArchive(File.OpenRead(path))) Check(zip.Entries.Count == 2, "staged ZIP decodes");
        try { offer.Stop(); throw new Exception("clear error missing"); }
        catch (COMException) { }
        Check(File.Exists(path), "failed clear preserves offered file");
        failClear = false;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { offer.Stop(); throw new Exception("cleanup error missing"); } catch (IOException) { }
            Check(File.Exists(path), "locked staged file retained for cleanup retry");
        }
        offer.Stop(); offer.Stop();
        Check(!File.Exists(path) && clears == 2 && File.ReadAllText(unrelated) == "keep", "stop clears then cleans exact file; idempotent; unrelated preserved");
        var unused = new StagedZipOffer(snapshot, root, _ => throw new Exception("unused published"),
            _ => throw new Exception("unused queried"), () => throw new Exception("unused cleared"));
        unused.Stop(); unused.Stop();
        try { unused.Retry(); throw new Exception("stopped retry accepted"); } catch (ObjectDisposedException) { }
        var replacement = new StagedZipOffer(TranscribeClipboardFixture.Create(), root, _ => 0, _ => 1, () => throw new Exception("replacement cleared"));
        replacement.Retry(); var otherPath = replacement.StagedPath!;
        var extra = Path.Combine(Path.GetDirectoryName(otherPath)!, "other.txt"); File.WriteAllText(extra, "keep");
        replacement.Stop();
        Check(!File.Exists(otherPath) && File.Exists(extra), "replacement clipboard and unrelated sibling preserved");
        File.Delete(extra); Directory.Delete(Path.GetDirectoryName(otherPath)!);
        bool stillReferenced = true;
        var copiedPath = new StagedZipOffer(snapshot, root, _ => 0, _ => 1,
            () => throw new Exception("replacement cleared"), referenced: _ => stillReferenced);
        copiedPath.Retry();
        try { copiedPath.Stop(); throw new Exception("referenced ZIP deleted"); } catch (IOException) { }
        Check(File.Exists(copiedPath.StagedPath), "replacement referencing same ZIP preserves staged file");
        stillReferenced = false; copiedPath.Stop();
        var unknown = new StagedZipOffer(TranscribeClipboardFixture.Create(), root, _ => 0,
            _ => unchecked((int)0x800401D0), () => throw new Exception("unknown owner cleared"));
        unknown.Retry();
        try { unknown.Stop(); throw new Exception("ownership error missing"); } catch (COMException) { }
        Check(File.Exists(unknown.StagedPath), "ownership failure retains staged path");
        // Test-only explicit cleanup: no real clipboard object was published.
        File.Delete(unknown.StagedPath!); Directory.Delete(Path.GetDirectoryName(unknown.StagedPath!)!);
        File.Delete(unrelated); Directory.Delete(root);
        Console.WriteLine("PASS partial staging/publication failure and retry; native CF_HDROP; safe stop failure/replacement/cleanup");
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(nint drop, uint index, System.Text.StringBuilder? name, uint size);
    private static void Reject(OleDataObject data, FORMATETC format, int result)
    {
        Check(data.QueryGetData(ref format) == result, "QueryGetData rejects invalid request");
        try { data.GetData(ref format, out _); throw new Exception("GetData accepted invalid request"); }
        catch (COMException error) { Check(error.HResult == result, "GetData consistent HRESULT"); }
    }
    private static byte[] Read(OleDataObject data, FORMATETC format)
    {
        data.GetData(ref format, out var medium);
        return ReadMedium(medium);
    }
    private static byte[] ReadMedium(STGMEDIUM medium)
    {
        try
        {
            Check(medium.tymed == TYMED.TYMED_ISTREAM && medium.pUnkForRelease == null, "stream medium ownership");
            var stream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
            try
            {
                stream.Stat(out var stat, 1); var bytes = new byte[checked((int)stat.cbSize)];
                var count = Marshal.AllocCoTaskMem(4);
                try { stream.Read(bytes, bytes.Length, count); Check(Marshal.ReadInt32(count) == bytes.Length, "full stream read"); }
                finally { Marshal.FreeCoTaskMem(count); }
                return bytes;
            }
            finally { Marshal.ReleaseComObject(stream); }
        }
        finally { ReleaseStgMedium(ref medium); }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoMarshalInterThreadInterfaceInStream(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] OleDataObject data, out nint stream);
    [DllImport("ole32.dll")] private static extern int CoGetInterfaceAndReleaseStream(nint stream, ref Guid iid, out nint data);
    private static T Method<T>(nint pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Query(nint self, ref FORMATETC format);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Get(nint self, ref FORMATETC format, out STGMEDIUM medium);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Enumerate(nint self, DATADIR direction, out nint enumerator);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Next(nint self, int count,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] FORMATETC[] formats, out int fetched);
}
