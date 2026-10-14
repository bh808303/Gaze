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
    private readonly Action undo, finish, cancel;
    private readonly Func<bool> requestClose;
    private bool closing;
    private TranscribeCapture? displayed;
    public TranscribeWindow(Action undo, Action finish, Action cancel, Func<bool> requestClose)
    {
        PanelWindow.EnsureHostApplication();
        this.undo = undo; this.finish = finish; this.cancel = cancel; this.requestClose = requestClose;
        InitializeComponent();
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
        Closing += (_, e) =>
        {
            if (closing) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => this.requestClose());
        };
    }
    public nint NativeHandle => new WindowInteropHelper(this).EnsureHandle();
    public void Refresh(TranscribeSession session, bool busy, string? status = null)
    {
        CountText.Text = $"{session.Count} capture{(session.Count == 1 ? "" : "s")}";
        UndoButton.IsEnabled = !busy && session.State == TranscribeState.Capturing && session.Count > 0;
        DoneButton.IsEnabled = CancelButton.IsEnabled = CloseButton.IsEnabled = !busy;
        DoneButton.Content = session.State == TranscribeState.ExportFailed ? "Retry" : "Done";
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
    public void CloseAfterFinish() { closing = true; Close(); }
    public void CheckFinishActions(Func<int> finished)
    {
        int before = finished();
        Hide(); BringToFront();
        if (!IsVisible || finished() != before) throw new InvalidOperationException("Restoring Transcribe ended the session or left it hidden.");
        WindowState = WindowState.Minimized; BringToFront();
        if (WindowState == WindowState.Minimized || finished() != before) throw new InvalidOperationException("Minimized Transcribe did not restore safely.");
        CloseClick(this, new RoutedEventArgs());
        if (finished() != before + 1) throw new InvalidOperationException("Transcribe cross did not finish the session.");
        DoneClick(this, new RoutedEventArgs());
        if (finished() != before + 2) throw new InvalidOperationException("Transcribe Done did not finish the session.");
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
    private void CloseClick(object sender, RoutedEventArgs e) => finish();
    private void UndoClick(object sender, RoutedEventArgs e) => undo();
    private void DoneClick(object sender, RoutedEventArgs e) => finish();
    private void CancelClick(object sender, RoutedEventArgs e) => cancel();
}
