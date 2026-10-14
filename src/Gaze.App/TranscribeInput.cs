using System.Runtime.InteropServices;

namespace Gaze.App;

internal sealed record TranscribePointer(int Message, Point Point, nint Window = 0, string Title = "", Rectangle Monitor = default, bool OwnWindow = false);

// The hook only copies small input records and queues work. Capture/encoding never
// runs inside a hook callback. All other mouse messages retain their normal path.
internal sealed class TranscribeInput : IDisposable
{
    private readonly Native.HookCallback callback;
    private readonly Action<TranscribePointer> queue;
    private nint hook;
    internal TranscribeInput(Action<TranscribePointer> queue)
    {
        this.queue = queue; callback = Handle;
        hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot install Transcribe middle-button handler.");
    }
    private nint Handle(int code, nint message, nint data)
    {
        if (code >= 0 && (message == 0x207 || message == 0x208 || message == 0x200))
        {
            var point = Marshal.PtrToStructure<Point>(data);
            if (message == 0x207)
            {
                // Snapshot at button-down, before queued UI work or another window
                // can change the target/title beneath these physical coordinates.
                var window = Native.GetAncestor(Native.WindowFromPoint(point), 2);
                Native.GetWindowThreadProcessId(window, out var pid);
                queue(new((int)message, point, window, Native.Title(window), Screen.FromPoint(point).Bounds, pid == Environment.ProcessId));
            }
            else queue(new((int)message, point));
            if (message is 0x207 or 0x208) return 1;
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void Dispose()
    {
        if (hook == 0) return;
        Native.UnhookWindowsHookEx(hook); hook = 0;
    }
}
