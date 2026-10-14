using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Gaze.App;



internal sealed class TranscribeScreenshotSource : IDisposable
{
    // Each request owns a new session: never return a cached frame from a prior gesture.
    internal static async Task<byte[]> CaptureWindowAsync(nint window)
    {
        if (window == 0 || !Native.IsWindow(window) || !Native.IsWindowVisible(window) || Native.IsIconic(window))
            throw new ArgumentException("The selected window is unavailable.", nameof(window));
        using var source = new TranscribeScreenshotSource(window, false, 30);
        return Encode(await source.WaitForFrameAsync(), null);
    }

    internal static async Task<byte[]> CaptureRectangleAsync(Rectangle selection, Rectangle startingMonitor)
    {
        var clipped = Rectangle.Intersect(selection, startingMonitor);
        if (clipped.Width <= 0 || clipped.Height <= 0) throw new ArgumentException("Selection has no pixels on the starting monitor.", nameof(selection));
        var monitor = Screen.AllScreens.SingleOrDefault(screen => screen.Bounds == startingMonitor)
            ?? throw new ArgumentException("Starting monitor is no longer available.", nameof(startingMonitor));
        using var source = new TranscribeScreenshotSource(Native.MonitorHandle(monitor), true, 30);
        var frame = await source.WaitForFrameAsync();
        clipped.Offset(-startingMonitor.Left, -startingMonitor.Top);
        if (!new Rectangle(0, 0, frame.Width, frame.Height).Contains(clipped))
            throw new InvalidOperationException("Monitor dimensions changed during capture; retry the gesture.");
        return Encode(frame, clipped);
    }

    // Pulling is explicitly supported by WGC. A one-shot screenshot does not
    // need event-driven FPS throttling: keep trying after resize even if the
    // replacement frame's notification arrived during the previous copy.
    private Task<CapturedFrame> WaitForFrameAsync()
        => TranscribeFramePump.WaitAsync(async () =>
        {
            await CopyNextFrameAsync().ConfigureAwait(false);
            return Latest;
        }, () => Error != null || Closed
            ? new InvalidOperationException(Error ?? "Capture target closed.") : null);
    private static byte[] Encode(CapturedFrame frame, Rectangle? crop)
    {
        using var bitmap = new Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
        try
        {
            for (int y = 0; y < frame.Height; y++)
                Marshal.Copy(frame.Pixels, y * frame.Width * 4, data.Scan0 + y * data.Stride, frame.Width * 4);
        }
        finally { bitmap.UnlockBits(data); }
        using var image = bitmap.Clone(crop ?? new Rectangle(0, 0, frame.Width, frame.Height), bitmap.PixelFormat);
        using var output = new MemoryStream();
        image.Save(output, System.Drawing.Imaging.ImageFormat.Png);
        return output.ToArray();
    }
    private IDirect3DDevice device = null!;
    private Direct3D11CaptureFramePool pool = null!;
    private GraphicsCaptureSession session = null!;
    private readonly TranscribeCaptureResources resources;
    private CapturedFrame? latest;
    private volatile bool disposed;
    private GraphicsCaptureItem item = null!;
    internal CapturedFrame? Latest => Volatile.Read(ref latest);
    internal bool Closed { get; private set; }
    internal string? Error { get; private set; }
    internal Size NativeSize => new(item.Size.Width, item.Size.Height);

    internal TranscribeScreenshotSource(nint handle, bool monitor, int fps, bool cursor = false, Action? beforeStart = null)
    {
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
        resources = TranscribeCaptureResources.Acquire(acquired =>
        {
            device = CreateDevice(); acquired.Own(device.Dispose);
            item = CreateItem(handle, monitor);
            if (item.Size.Width <= 0 || item.Size.Height <= 0)
                throw new InvalidOperationException($"WGC capture item has unusable dimensions {item.Size.Width}x{item.Size.Height}; native validation requires an interactive desktop with local permissions.");
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            acquired.Own(pool.Dispose);
            session = pool.CreateCaptureSession(item); acquired.Own(session.Dispose);
            session.IsCursorCaptureEnabled = cursor;
            item.Closed += ItemClosed; acquired.Own(() => item.Closed -= ItemClosed);
            try { beforeStart?.Invoke(); session.StartCapture(); }
            catch { disposed = true; throw; }
        });
    }

    // Diagnostic only: positive cursor control and deterministic resize between
    // pool allocation and first capture, without changing production callers.
    internal static async Task<byte[]> CaptureForProbeAsync(nint handle, bool monitor, bool cursor, Action? beforeStart, Rectangle? crop)
    {
        using var source = new TranscribeScreenshotSource(handle, monitor, 30, cursor, beforeStart);
        return Encode(await source.WaitForFrameAsync(), crop);
    }
    private async Task CopyNextFrameAsync()
    {
        if (disposed) return;
        SizeInt32? resize = null;
        try
        {
            using var frame = pool.TryGetNextFrame();
            if (frame == null) return;
            using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().ConfigureAwait(false);
            if (disposed) return;
            int width = frame.ContentSize.Width, height = frame.ContentSize.Height;
            if (width <= 0 || height <= 0) return;
            if (!TranscribeFrameSafety.CanPublish(width, height, bitmap.PixelWidth, bitmap.PixelHeight))
            {
                Volatile.Write(ref latest, null);
                resize = frame.ContentSize;
                return;
            }
            var buffer = new Windows.Storage.Streams.Buffer(checked((uint)(bitmap.PixelWidth * bitmap.PixelHeight * 4)));
            bitmap.CopyToBuffer(buffer);
            var bytes = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
            Volatile.Write(ref latest, new CapturedFrame(bytes, width, height));
        }
        catch (Exception e) { if (!disposed && !Closed) Error = e.Message; }
        finally
        {
            // The using declarations in the try have returned the frame and
            // surface before Recreate discards the old pool buffers.
            if (resize.HasValue && !disposed)
            {
                try { pool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, resize.Value); }
                catch (Exception e) { if (!disposed) Error = e.Message; }
            }
        }
    }
    private void ItemClosed(GraphicsCaptureItem sender, object args) => Closed = true;
    public void Dispose() { if (disposed) return; disposed = true; resources.Dispose(); }

    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint levels, uint count, uint sdkVersion, out nint device, out uint level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint inspectable);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string text, uint length, out nint hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint hstring);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateCaptureItem(nint factory, nint handle, ref Guid iid, out nint item);
    private static IDirect3DDevice CreateDevice()
    {
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out var native, out _, out var context));
        nint dxgi = 0, inspectable = 0;
        try
        {
            var iid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally { if (inspectable != 0) Marshal.Release(inspectable); if (dxgi != 0) Marshal.Release(dxgi); Marshal.Release(context); Marshal.Release(native); }
    }
    private static GraphicsCaptureItem CreateItem(nint handle, bool monitor)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(name, (uint)name.Length, out var hstring));
        nint factory = 0, item = 0;
        try
        {
            var iid = new Guid("3628e81b-3cac-4c60-b7f4-23ce0e0c3356");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref iid, out factory));
            var method = Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), (monitor ? 4 : 3) * nint.Size);
            var create = Marshal.GetDelegateForFunctionPointer<CreateCaptureItem>(method);
            var itemIid = new Guid("79c3f95b-31f7-4ec2-a464-632ef5d30760");
            Marshal.ThrowExceptionForHR(create(factory, handle, ref itemIid, out item));
            return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(item);
        }
        finally { if (item != 0) Marshal.Release(item); if (factory != 0) Marshal.Release(factory); WindowsDeleteString(hstring); }
    }
}


