using Gaze.Core;
using Gaze.Fluent;
namespace Gaze.App;
internal sealed class TrayPanel : IDisposable, IWin32Window
{
    private readonly PanelWindow window;
    private uint displayAffinity;
    public nint Handle => window.NativeHandle;
    internal TrayPanel(Func<Settings> getSettings, Action<Settings> apply, Action action, Action cancel, Action<string> open, Action folder, Action<string> copy, Action quit, Action cleanup)
    {
        window = new PanelWindow(() =>
        {
            var value = getSettings(); return new PanelOptions { Mode = value.Mode, Canvas = value.WindowCanvas, Folder = value.Folder, FrameRate = value.FrameRate, Compression = value.Compression, Modifiers = value.Modifiers, Key = (int)value.Key, ShortcutText = value.Shortcut, AutoCopy = value.AutoCopy, SplitAt29Mb = value.SplitAt29Mb, ClickHighlight = value.ClickHighlight, DoubleEscape = value.DoubleEscape };
        }, options => apply(new Settings { Mode = options.Mode, WindowCanvas = options.Canvas, Folder = options.Folder, FrameRate = options.FrameRate, Compression = options.Compression, Modifiers = options.Modifiers, Key = (Keys)options.Key, AutoCopy = options.AutoCopy, SplitAt29Mb = options.SplitAt29Mb, ClickHighlight = options.ClickHighlight, DoubleEscape = options.DoubleEscape }), action, cancel, open, folder, copy, quit, cleanup);
        Native.SetWindowDisplayAffinity(Handle, 0);
    }
    internal void RefreshState(SessionState state, TimeSpan duration, bool busy, bool hasFile)
    {
        uint affinity = state == SessionState.Idle && !busy ? 0u : 0x11u;
        if (affinity != displayAffinity)
        {
            if (!Native.SetWindowDisplayAffinity(Handle, affinity)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Could not update panel capture exclusion.");
            displayAffinity = affinity;
        }
        window.RefreshState(state, duration, busy);
    }
    internal void RefreshRecordings() => window.RefreshRecordings();
    internal void EditSettings() => window.EditSettings();
    internal void CheckCompression() => window.CheckCompression();
    internal void CheckSplitting() => window.CheckSplitting();
    internal void Hide() => window.Hide();
    internal void Show() => window.Show();
    internal void RunPreview(string path) => window.RunPreview(path);
    internal static void RunHost(ApplicationContext context)
    {
        context.ThreadExit += (_, _) => PanelWindow.StopHostDispatcher();
        PanelWindow.RunHostDispatcher();
    }
    internal void CheckPlacement(string directory)
    {
        Directory.CreateDirectory(directory);
        var screens = Screen.AllScreens;
        var evidence = new List<object>(); int index = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 800 };
        void OpenNext()
        {
            var area = screens[index].WorkingArea;
            OpenAt(new Point(area.Left + area.Width / 2, area.Top + area.Height / 2));
        }
        timer.Tick += (_, _) =>
        {
            var area = screens[index].WorkingArea; var bounds = Native.WindowBounds(Handle);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bitmap.Save(Path.Combine(directory, $"monitor-{index}.png"));
            if (!area.Contains(bounds)) throw new InvalidOperationException("Gaze panel extends outside the screen working area.");
            if (Math.Abs(bounds.Right - area.Right) > 1 || Math.Abs(bounds.Bottom - area.Bottom) > 1)
                throw new InvalidOperationException("Gaze panel is not aligned to the bottom-right working-area corner.");
            int colored = 0;
            for (int y = 0; y < bitmap.Height; y += 8)
                for (int x = 0; x < bitmap.Width; x += 8)
                { var color = bitmap.GetPixel(x, y); if (color.R > color.G + 40 || color.B > color.R + 40) colored++; }
            if (colored < 10) throw new InvalidOperationException("Gaze panel did not visibly render its colored controls.");
            // Captures the real desktop window through the normal WinForms message loop.
            evidence.Add(new { screens[index].DeviceName, Dpi = Native.GetDpiForWindow(Handle), Area = area, Bounds = bounds, Fits = area.Contains(bounds), Layout = window.LayoutEvidence() });
            if (++index < screens.Length) OpenNext();
            else { timer.Stop(); File.WriteAllText(Path.Combine(directory, "placement.json"), System.Text.Json.JsonSerializer.Serialize(evidence)); PanelWindow.StopHostDispatcher(); }
        };
        OpenNext(); timer.Start(); PanelWindow.RunHostDispatcher();
    }
    internal void OpenAt(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        window.Hide();
        window.RefreshRecordings();
        // Establish the tray-side position while hidden, including on the first opening.
        var initial = Native.WindowBounds(Handle);
        int initialWidth = Math.Min(initial.Width, area.Width), initialHeight = Math.Min(initial.Height, area.Height);
        Native.SetWindowPos(Handle, 0, area.Right - initialWidth, area.Bottom - initialHeight, initialWidth, initialHeight, 0x14);
        var dpi = Native.GetDpiForWindow(Handle) / 96.0;
        window.MaxWidth = area.Width / dpi; window.MaxHeight = area.Height / dpi;
        window.UpdateLayout();
        var bounds = Native.WindowBounds(Handle);
        int width = Math.Min(bounds.Width, area.Width), height = Math.Min(bounds.Height, area.Height);
        int x = area.Right - width;
        int y = area.Bottom - height;
        Native.SetWindowPos(Handle, 0, x, y, width, height, 0x14);
        window.ApplyTemplate(); window.UpdateLayout(); window.Opacity = 1;
        window.Show();
        Gaze.Fluent.PanelWindow.QueueOnHost(() =>
        {
            window.UpdateLayout();
            var shown = Native.WindowBounds(Handle);
            int shownWidth = Math.Min(shown.Width, area.Width), shownHeight = Math.Min(shown.Height, area.Height);
            Native.SetWindowPos(Handle, 0, area.Right - shownWidth, area.Bottom - shownHeight, shownWidth, shownHeight, 0x14);
            window.Activate();
        });
    }
    public void Dispose() => window.DisposePanel();
}
