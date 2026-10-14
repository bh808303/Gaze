using System.IO.Compression;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Gaze.App;

// Probe-only producer. Every returned STGMEDIUM belongs to the consumer.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class VirtualZipDataObject : OleDataObject
{
    private readonly byte[] payload;
    private readonly byte[] descriptor;
    private readonly FORMATETC[] formats;
    internal VirtualZipDataObject(string name, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length >= 260 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("A single valid virtual filename is required.", nameof(name));
        payload = (byte[])bytes.Clone();
        // FILEGROUPDESCRIPTORW: count (4), FILEDESCRIPTORW (592); filename at offset 72.
        descriptor = new byte[596];
        using var writer = new BinaryWriter(new MemoryStream(descriptor), Encoding.Unicode);
        writer.Write(1u); writer.Write(0x80000044u); // FD_UNICODE | FD_ATTRIBUTES | FD_FILESIZE
        writer.BaseStream.Position = 4 + 36; writer.Write(0x80u); // FILE_ATTRIBUTE_NORMAL
        writer.BaseStream.Position = 4 + 64; writer.Write(0u); writer.Write((uint)payload.Length);
        writer.BaseStream.Position = 4 + 72; writer.Write(Encoding.Unicode.GetBytes(name + "\0"));
        formats = [Format("FileGroupDescriptorW", -1, TYMED.TYMED_HGLOBAL), Format("FileContents", 0, TYMED.TYMED_ISTREAM)];
    }
    private static FORMATETC Format(string name, int index, TYMED medium) => new()
    { cfFormat = unchecked((short)RegisterClipboardFormat(name)), dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = medium };
    public int QueryGetData(ref FORMATETC format)
    {
        var formatId = format.cfFormat;
        var supported = formats.FirstOrDefault(x => x.cfFormat == formatId);
        if (supported.cfFormat == 0) return unchecked((int)0x80040064); // DV_E_FORMATETC
        if (format.dwAspect != DVASPECT.DVASPECT_CONTENT) return unchecked((int)0x8004006B);
        if (format.lindex != supported.lindex) return unchecked((int)0x80040068);
        if (format.ptd != 0) return unchecked((int)0x80040065);
        return (format.tymed & supported.tymed) != 0 ? 0 : unchecked((int)0x80040069);
    }
    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        medium = default;
        Marshal.ThrowExceptionForHR(QueryGetData(ref format));
        if (format.cfFormat == formats[0].cfFormat)
        {
            var memory = GlobalAlloc(0x42, (nuint)descriptor.Length); // movable, zero initialized
            if (memory == 0) throw new OutOfMemoryException();
            var pointer = GlobalLock(memory);
            if (pointer == 0) { GlobalFree(memory); throw new OutOfMemoryException(); }
            try { Marshal.Copy(descriptor, 0, pointer, descriptor.Length); }
            finally { GlobalUnlock(memory); }
            medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = memory };
            return;
        }
        Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0, true, out var streamPointer));
        try
        {
            var stream = (IStream)Marshal.GetObjectForIUnknown(streamPointer);
            try { stream.Write(payload, payload.Length, 0); stream.Seek(0, 0, 0); }
            finally { Marshal.ReleaseComObject(stream); }
            medium = new STGMEDIUM { tymed = TYMED.TYMED_ISTREAM, unionmember = streamPointer };
        }
        catch { Marshal.Release(streamPointer); throw; }
    }
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => direction == DATADIR.DATADIR_GET
        ? new FormatEnumerator(formats) : throw new COMException("Read-only data object", unchecked((int)0x80004001));
    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Use GetData", unchecked((int)0x80040069));
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; output.ptd = 0; return 0x40030; }
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) => throw new COMException("Read-only", unchecked((int)0x80004001));
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return unchecked((int)0x80040003); }
    public void DUnadvise(int connection) => throw new COMException("No advisory connections", unchecked((int)0x80040003));
    public int EnumDAdvise(out IEnumSTATDATA enumerator) { enumerator = null!; return unchecked((int)0x80040003); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(nint memory, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, out nint stream);

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class FormatEnumerator(FORMATETC[] formats, int position = 0) : IEnumFORMATETC
    {
        private int index = position;
        public int Next(int count, FORMATETC[] output, int[] fetched)
        {
            if (count < 0 || output.Length < count || (count != 1 && fetched == null)) return unchecked((int)0x80070057);
            int copied = 0;
            while (copied < count && index < formats.Length) output[copied++] = formats[index++];
            if (fetched != null && fetched.Length > 0) fetched[0] = copied;
            return copied == count ? 0 : 1;
        }
        public int Skip(int count)
        {
            if (count < 0) return unchecked((int)0x80070057);
            var remaining = formats.Length - index; index += Math.Min(count, remaining); return count <= remaining ? 0 : 1;
        }
        public int Reset() { index = 0; return 0; }
        public void Clone(out IEnumFORMATETC newEnum) => newEnum = new FormatEnumerator(formats, index);
    }
}

internal sealed class TranscribeClipboardOwner : IDisposable
{
    private readonly Thread thread;
    private readonly Control dispatcher;
    private readonly Func<OleDataObject, int> publish;
    private readonly Func<OleDataObject, int> isCurrent;
    private readonly Func<int> clear;
    private VirtualZipDataObject? retained;
    private VirtualZipDataObject? published;
    private bool disposed;

    internal TranscribeClipboardOwner(Func<OleDataObject, int>? publish = null,
        Func<OleDataObject, int>? isCurrent = null, Func<int>? clear = null)
    {
        this.publish = publish ?? OleSetClipboard;
        this.isCurrent = isCurrent ?? OleIsCurrentClipboard;
        this.clear = clear ?? (() => OleSetClipboard(null));
        var ready = new TaskCompletionSource<Control>(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            var initialized = false;
            try
            {
                Marshal.ThrowExceptionForHR(OleInitialize(0)); initialized = true;
                using var control = new Control();
                _ = control.Handle;
                // Signal only once the pump actually dispatches a queued callback.
                control.BeginInvoke(() => ready.TrySetResult(control));
                Application.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
            finally { if (initialized) OleUninitialize(); }
        }) { IsBackground = true, Name = "Gaze virtual ZIP OLE owner" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        dispatcher = ready.Task.GetAwaiter().GetResult();
    }
    public void Invoke(Action action) => Invoke(() => { action(); return true; });
    public T Invoke<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Environment.CurrentManagedThreadId == thread.ManagedThreadId ? action() : (T)dispatcher.Invoke(action)!;
    }
    internal void Publish(string name, byte[] bytes) => Invoke(() =>
    {
        var next = new VirtualZipDataObject(name, bytes);
        retained = next;
        Marshal.ThrowExceptionForHR(publish(next));
        published = next;
    });
    internal void Retry() => Invoke(() =>
    {
        if (retained == null) throw new InvalidOperationException("No probe payload to retry.");
        Marshal.ThrowExceptionForHR(publish(retained)); published = retained;
    });
    public void Dispose()
    {
        if (disposed) return;
        try
        {
            Invoke(() =>
            {
                try
                {
                    // Never clear a replacement clipboard owned by another application.
                    if (published != null && isCurrent(published) == 0) Marshal.ThrowExceptionForHR(clear());
                }
                finally { retained = null; published = null; Application.ExitThread(); }
            });
        }
        finally { disposed = true; if (Environment.CurrentManagedThreadId != thread.ManagedThreadId) thread.Join(); }
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int OleSetClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject? data);
    [DllImport("ole32.dll")] private static extern int OleIsCurrentClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject data);
}

internal static class TranscribeClipboardFixture
{
    public static byte[] Create()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using var image = new Bitmap(480, 120);
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Segoe UI", 18))
            { graphics.Clear(Color.White); graphics.DrawString("T02-IN-MEMORY-7319", font, Brushes.Black, 12, 30); }
            using (var entry = archive.CreateEntry("000001_probe.png").Open()) image.Save(entry, ImageFormat.Png);
            using var instructions = new StreamWriter(archive.CreateEntry("instructions.txt").Open(), Encoding.UTF8);
            instructions.Write("Transcribe PNGs in filename order. Merge overlaps, remove duplicates, preserve headings, and flag unreadable text without guessing.");
        }
        return output.ToArray();
    }
}

internal static class TranscribeClipboardProbe
{
    private const string StopEvent = "Local\\Gaze.TranscribeClipboardProbe.Stop";
    internal static void Stop()
    {
        try { using var stop = EventWaitHandle.OpenExisting(StopEvent); stop.Set(); }
        catch (WaitHandleCannotBeOpenedException) { Environment.ExitCode = 1; }
    }
    internal static void Run()
    {
        using var singleton = new Mutex(true, "Local\\Gaze.TranscribeClipboardProbe", out var first);
        if (!first) { Environment.ExitCode = 1; return; }
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEvent);
        using var owner = new TranscribeClipboardOwner();
        // Generate before any clipboard operation; generation failure cannot replace existing data.
        var fixture = TranscribeClipboardFixture.Create();
        using var form = new Form { Text = "Gaze T02 virtual ZIP probe", ClientSize = new Size(530, 190), StartPosition = FormStartPosition.CenterScreen };
        var status = new Label { Left = 16, Top = 16, Width = 495, Height = 100 };
        var retry = new Button { Text = "Retry", Left = 16, Top = 130, Enabled = false };
        var close = new Button { Text = "Stop probe", Left = 120, Top = 130, Width = 110 };
        form.Controls.AddRange([status, retry, close]);
        void Offer(Action action)
        {
            try
            {
                action(); retry.Enabled = false;
                status.Text = "Offered gaze-transcribe-probe.zip in memory.\nPaste manually into Claude Desktop; compatibility is unvalidated.\nThe separate STA stays alive until Stop/close. No disk fallback.\nStop removes this offering only if it still owns the clipboard.";
            }
            catch (Exception error) { retry.Enabled = true; status.Text = "Offer failed; bytes retained in memory. Retry available.\n" + error.Message; }
        }
        retry.Click += (_, _) => Offer(owner.Retry);
        close.Click += (_, _) => form.Close();
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) => { if (stop.WaitOne(0)) form.Close(); };
        // This is the only explicit opt-in path that publishes to the real clipboard.
        form.Shown += (_, _) => { Offer(() => owner.Publish("gaze-transcribe-probe.zip", fixture)); timer.Start(); };
        Application.Run(form);
    }
}
