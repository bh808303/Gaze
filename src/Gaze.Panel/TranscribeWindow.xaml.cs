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
        CountText.Text = $"{session.Count} capture{(session.Count == 1 ? "" : "s")} / {session.TotalPngBytes:N0} PNG bytes";
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
    public void CloseAfterFinish() { closing = true; Close(); }
    public void RenderEvidence(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private void HeaderDrag(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void UndoClick(object sender, RoutedEventArgs e) => undo();
    private void DoneClick(object sender, RoutedEventArgs e) => finish();
    private void NextClick(object sender, RoutedEventArgs e) => nextBatch();
    private void RetryClick(object sender, RoutedEventArgs e) => retry();
}
