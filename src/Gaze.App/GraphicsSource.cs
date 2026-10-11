using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Gaze.App;

internal sealed record CapturedFrame(byte[] Pixels, int Width, int Height);

internal sealed class GraphicsSource : IDisposable
{
    private readonly IDirect3DDevice device;
    private readonly Direct3D11CaptureFramePool pool;
    private readonly GraphicsCaptureSession session;
    private readonly int fps;
    private CapturedFrame? latest;
    private long lastCopy;
    private int copying;
    private volatile bool disposed;
    private readonly GraphicsCaptureItem item;
    internal CapturedFrame? Latest => Volatile.Read(ref latest);
    internal bool Closed { get; private set; }
    internal string? Error { get; private set; }
    internal Size NativeSize => new(item.Size.Width, item.Size.Height);

    internal GraphicsSource(nint handle, bool monitor, int fps)
    {
        this.fps = fps; device = CreateDevice();
        item = CreateItem(handle, monitor);
        pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        session = pool.CreateCaptureSession(item); session.IsCursorCaptureEnabled = true;
        item.Closed += ItemClosed;
        pool.FrameArrived += Arrived; session.StartCapture();
    }
    private async void Arrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (disposed || Interlocked.CompareExchange(ref copying, 1, 0) != 0) return;
        SizeInt32? resize = null;
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;
            var now = Environment.TickCount64;
            if (now - lastCopy < 1000 / fps) return;
            lastCopy = now;
            using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            if (disposed) return;
            int width = Math.Min(bitmap.PixelWidth, frame.ContentSize.Width), height = Math.Min(bitmap.PixelHeight, frame.ContentSize.Height);
            if (width <= 0 || height <= 0) return;
            var buffer = new Windows.Storage.Streams.Buffer(checked((uint)(bitmap.PixelWidth * bitmap.PixelHeight * 4)));
            bitmap.CopyToBuffer(buffer);
            var bytes = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
            if (width != bitmap.PixelWidth || height != bitmap.PixelHeight)
            {
                var cropped = new byte[checked(width * height * 4)];
                for (int y = 0; y < height; y++) System.Buffer.BlockCopy(bytes, y * bitmap.PixelWidth * 4, cropped, y * width * 4, width * 4);
                bytes = cropped;
            }
            Volatile.Write(ref latest, new CapturedFrame(bytes, width, height));
            if (bitmap.PixelWidth != frame.ContentSize.Width || bitmap.PixelHeight != frame.ContentSize.Height) resize = frame.ContentSize;
        }
        catch (Exception e) { if (!disposed && !Closed) Error = e.Message; }
        finally
        {
            if (resize.HasValue && !disposed)
            {
                try { pool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, resize.Value); }
                catch (Exception e) { if (!disposed) Error = e.Message; }
            }
            Volatile.Write(ref copying, 0);
        }
    }
    private void ItemClosed(GraphicsCaptureItem sender, object args) => Closed = true;
    public void Dispose() { if (disposed) return; disposed = true; item.Closed -= ItemClosed; pool.FrameArrived -= Arrived; session.Dispose(); pool.Dispose(); device.Dispose(); }

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
