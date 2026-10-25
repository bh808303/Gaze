using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Gaze.Core;
using Wpf.Ui.Controls;

namespace Gaze.Fluent;

public partial class TranscribeWindow : FluentWindow
{
    private readonly Action undo, finish, nextBatch, retry;
    private readonly Func<bool> requestClose;
    private bool closing;
    private TranscribeCapture? displayed;
    public TranscribeWindow(Action undo, Action finish, Func<bool> requestClose, Action nextBatch, Action retry)
    {
        PanelWindow.EnsureHostApplication();
        this.undo = undo; this.finish = finish; this.nextBatch = nextBatch; this.retry = retry; this.requestClose = requestClose;
        InitializeComponent();
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
        Closing += (_, e) =>
        {
            if (closing) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => this.requestClose());
        };
    }
    public string ClipboardStatus => StatusText.Text;
    public string BatchSummary => CountText.Text;
    public bool ZipEnabled => DoneButton.IsEnabled;
    public bool RetryVisible => RetryButton.Visibility == Visibility.Visible;
    public bool HasThumbnail => Thumbnail.Source != null;
    public nint NativeHandle => new WindowInteropHelper(this).EnsureHandle();
    public void Refresh(TranscribeSession session, bool busy, string? status = null, bool needsRetry = false)
    {
        CountText.Text = $"{session.Count} capture{(session.Count == 1 ? "" : "s")} / {FormatSize(session.TotalPngBytes)} PNG";
        UndoButton.IsEnabled = !busy && session.State == TranscribeState.Capturing && session.Count > 0;
        DoneButton.IsEnabled = !busy && session.Count > 0;
        NextButton.IsEnabled = true;
        RetryButton.IsEnabled = !busy;
        RetryButton.Visibility = needsRetry ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = status ?? session.LastExportError?.Message ?? "";
        if (ReferenceEquals(displayed, session.Latest)) return;
        displayed = session.Latest;
        if (displayed == null) Thumbnail.Source = null;
        else
        {
            using var stream = new MemoryStream(displayed.GetPngBytes());
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); Thumbnail.Source = image;
        }
        Empty.Visibility = displayed == null ? Visibility.Visible : Visibility.Collapsed;
    }
    public void BringToFront()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
    private static string FormatSize(long bytes) => bytes >= 1_000_000
        ? $"{bytes / 1_000_000d:0.##} MB" : bytes >= 1_000 ? $"{bytes / 1_000d:0.##} KB" : $"{bytes:N0} bytes";
    public void CloseAfterFinish() { closing = true; Close(); }
    public void CheckBatchActions(Func<int> zipped, Func<int> closed)
    {
        int beforeZip = zipped(), beforeClose = closed();
        Hide(); BringToFront();
        if (!IsVisible || zipped() != beforeZip || closed() != beforeClose)
            throw new InvalidOperationException("Restoring Transcribe changed the batch or left it hidden.");
        WindowState = WindowState.Minimized; BringToFront();
        if (WindowState == WindowState.Minimized || zipped() != beforeZip || closed() != beforeClose)
            throw new InvalidOperationException("Minimized Transcribe did not restore safely.");
        CloseClick(this, new RoutedEventArgs());
        if (closed() != beforeClose + 1 || zipped() != beforeZip)
            throw new InvalidOperationException("Transcribe cross must close without ZIP.");
        DoneClick(this, new RoutedEventArgs());
        if (zipped() != beforeZip + 1 || closed() != beforeClose + 1)
            throw new InvalidOperationException("Transcribe ZIP must export explicitly.");
    }
    public void RenderEvidence(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private void HeaderDrag(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void CloseClick(object sender, RoutedEventArgs e) => requestClose();
    private void UndoClick(object sender, RoutedEventArgs e) => undo();
    private void DoneClick(object sender, RoutedEventArgs e) => finish();
    private void NextClick(object sender, RoutedEventArgs e) => nextBatch();
    private void RetryClick(object sender, RoutedEventArgs e) => retry();
}
