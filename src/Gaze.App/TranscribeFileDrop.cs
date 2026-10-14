using System.Runtime.InteropServices;
using OleDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Gaze.App;

// Probe-only. Invoke all methods on the pumping OLE STA; never sweeps old instances.
internal sealed class StagedZipOffer
{
    private readonly byte[] bytes;
    private readonly string directory;
    private readonly Func<OleDataObject, int> publish;
    private readonly Func<OleDataObject, int> current;
    private readonly Func<int> clear;
    private readonly Action<string, byte[]> write;
    private readonly Func<string, bool> referenced;
    private readonly string fileName;
    private OleDataObject? data;
    private bool attempted, stopped, withdrawn;
    internal string? StagedPath { get; private set; }

    internal StagedZipOffer(byte[] bytes, string root, Func<OleDataObject, int> publish,
        Func<OleDataObject, int> current, Func<int> clear, Action<string, byte[]>? write = null,
        Func<string, bool>? referenced = null, string fileName = "gaze-transcribe-probe.zip")
    {
        if (Path.GetFileName(fileName) != fileName || string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Expected a ZIP leaf name.", nameof(fileName));
        this.fileName = fileName;
        this.bytes = (byte[])bytes.Clone();
        directory = Path.Combine(Path.GetFullPath(root), "gaze-transcribe-" + Guid.NewGuid().ToString("N"));
        this.publish = publish; this.current = current; this.clear = clear;
        this.referenced = referenced ?? (_ => false);
        this.write = write ?? ((path, payload) =>
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(payload); stream.Flush(true);
        });
    }
    internal void Retry()
    {
        ObjectDisposedException.ThrowIf(stopped || withdrawn, this);
        if (StagedPath == null)
        {
            if (Directory.Exists(directory)) throw new IOException("Staging directory already exists; refusing reuse.");
            Directory.CreateDirectory(directory);
            EnsureOrdinaryDirectory();
            StagedPath = Path.Combine(directory, fileName);
            try { write(StagedPath, bytes); }
            catch
            {
                // Keep path recorded if cleanup fails; never publish a partial ZIP.
                Cleanup();
                throw;
            }
            var drop = new DataObject();
            drop.SetFileDropList(new System.Collections.Specialized.StringCollection { StagedPath });
            data = (OleDataObject)drop;
        }
        if (data == null) throw new IOException("Incomplete staging requires Stop cleanup before a new probe.");
        attempted = true;
        Marshal.ThrowExceptionForHR(publish(data));
    }
    internal void Stop()
    {
        if (stopped) return;
        if (!withdrawn && attempted)
        {
            var result = current(data!);
            Marshal.ThrowExceptionForHR(result);
            if (result == 0) Marshal.ThrowExceptionForHR(clear());
            else if (result != 1) throw new IOException("Clipboard ownership is uncertain; staged ZIP retained.");
            else if (StagedPath != null && referenced(StagedPath))
                throw new IOException("Replacement clipboard still references this ZIP; copy other content before Stop.");
        }
        withdrawn = true;
        Cleanup();
        data = null; stopped = true;
    }
    private void EnsureOrdinaryDirectory()
    {
        // Refuse redirected directories, including ancestors, before writes/deletes.
        for (var item = new DirectoryInfo(directory); item != null; item = item.Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing redirected staging directory: " + item.FullName);
    }
    private void Cleanup()
    {
        if (!Directory.Exists(directory))
        {
            if (StagedPath != null) throw new IOException("Staging directory missing; cleanup cannot verify ownership.");
            return;
        }
        EnsureOrdinaryDirectory();
        if (StagedPath != null)
        {
            if (File.Exists(StagedPath) && (File.GetAttributes(StagedPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing redirected staged file.");
            File.Delete(StagedPath); StagedPath = null;
        }
        // Do not recursively delete or touch siblings, even inside our own directory.
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }
}

internal static class TranscribeFileDropProbe
{
    private const string StopEvent = "Local\\Gaze.TranscribeFileDropProbe.Stop";
    internal static void Stop()
    {
        try { using var stop = EventWaitHandle.OpenExisting(StopEvent); stop.Set(); }
        catch (WaitHandleCannotBeOpenedException) { Environment.ExitCode = 1; }
    }
    internal static void Run()
    {
        using var singleton = new Mutex(true, "Local\\Gaze.TranscribeFileDropProbe", out var first);
        if (!first) { Environment.ExitCode = 1; return; }
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEvent);
        using var owner = new TranscribeClipboardOwner();
        var bytes = TranscribeClipboardFixture.Create();
        var offer = owner.Invoke(() => new StagedZipOffer(bytes,
            Path.Combine(Path.GetTempPath(), "Gaze.TranscribeClipboardProbe"),
            OleSetClipboard, OleIsCurrentClipboard, () => OleSetClipboard(null), referenced: path =>
                Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Cast<string>().Any(value =>
                    string.Equals(Path.GetFullPath(value), path, StringComparison.OrdinalIgnoreCase))));
        using var form = new Form { Text = "Gaze T02 temporary ZIP file-drop probe", ClientSize = new Size(670, 260), StartPosition = FormStartPosition.CenterScreen };
        var status = new Label { Left = 16, Top = 16, Width = 635, Height = 180 };
        var retry = new Button { Text = "Retry", Left = 16, Top = 210 };
        var close = new Button { Text = "Stop probe", Left = 120, Top = 210, Width = 110 };
        form.Controls.AddRange([status, retry, close]);
        void Offer()
        {
            try
            {
                owner.Invoke(offer.Retry); retry.Enabled = false;
                status.Text = "Offered temporary ZIP file-drop. Keep running until paste/ingestion finishes.\n" +
                    owner.Invoke(() => offer.StagedPath) + "\nClaude compatibility is unvalidated. Stop withdraws our offering and deletes only this staged ZIP.";
            }
            catch (Exception error)
            {
                retry.Enabled = true;
                status.Text = "Offer failed; memory bytes retained. Retry available.\n" + error.Message + "\nStaged path: " + owner.Invoke(() => offer.StagedPath);
            }
        }
        retry.Click += (_, _) => Offer();
        close.Click += (_, _) => form.Close();
        form.FormClosing += (_, args) =>
        {
            try { owner.Invoke(offer.Stop); }
            catch (Exception error)
            {
                args.Cancel = true; retry.Enabled = false;
                status.Text = "Stop/cleanup failed; process and staged path retained. Retry Stop probe.\n" + error.Message + "\n" + owner.Invoke(() => offer.StagedPath);
            }
        };
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) => { if (stop.WaitOne(0)) { stop.Reset(); form.Close(); } };
        form.Shown += (_, _) => { Offer(); timer.Start(); };
        Application.Run(form);
    }
    [DllImport("ole32.dll")] private static extern int OleSetClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject? data);
    [DllImport("ole32.dll")] private static extern int OleIsCurrentClipboard([MarshalAs(UnmanagedType.Interface)] OleDataObject data);
}
