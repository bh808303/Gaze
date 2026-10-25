using System.Runtime.InteropServices;
using Gaze.Core;

namespace Gaze.App;

internal sealed record TranscribePointer(int Message, Point Point, nint Window = 0, string Title = "", Rectangle Monitor = default, bool OwnWindow = false, int Generation = 0);

// Hooks normalize physical triggers; capture/encoding runs only on the queued host path.
internal sealed class TranscribeInput : IDisposable
{
    private readonly Native.HookCallback mouseCallback, keyboardCallback;
    private readonly Action<TranscribePointer> queue;
    private readonly TranscribeTriggerPolicy policy;
    private nint mouseHook, keyboardHook;
    private System.Windows.Forms.Timer? drain;
    private bool stopped;
    private readonly Func<Point, TranscribePointer>? snapshot;
    internal int Generation { get; private set; }
    internal TranscribeInput(Action<TranscribePointer> queue, TranscribeTrigger trigger = TranscribeTrigger.MiddleMouse, bool installHooks = true, Func<Point, TranscribePointer>? snapshot = null)
    {
        this.queue = queue;
        this.snapshot = snapshot;
        policy = new(trigger, installHooks && Held(44));
        mouseCallback = Mouse; keyboardCallback = Keyboard;
        if (!installHooks) return;
        try
        {
            mouseHook = Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
            if (mouseHook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if (trigger == TranscribeTrigger.PrintScreen)
            {
                keyboardHook = Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
                if (keyboardHook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch { Unhook(); throw; }
    }
    private static bool Held(int key) => (Native.GetAsyncKeyState(key) & 0x8000) != 0;
    private nint Mouse(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var route = RouteMouse((int)message, Marshal.PtrToStructure<Point>(data));
            if (route.Consume) return 1;
        }
        return Native.CallNextHookEx(mouseHook, code, message, data);
    }
    private nint Keyboard(int code, nint message, nint data)
    {
        if (code >= 0 && (message == 0x100 || message == 0x104 || message == 0x101 || message == 0x105))
        {
            int key = Marshal.ReadInt32(data);
            bool hasPoint = Native.GetCursorPos(out var point);
            var route = RouteKey(key, message == 0x100 || message == 0x104,
                Held(16) || Held(17) || Held(18) || Held(91) || Held(92), point, hasPoint);
            if (route.Consume) return 1;
        }
        return Native.CallNextHookEx(keyboardHook, code, message, data);
    }
    internal TranscribeRoute RouteMouse(int message, Point point)
    {
        var route = policy.Mouse(message);
        if (route.Message != 0) Emit(route.Message, point);
        return route;
    }
    internal TranscribeRoute RouteKey(int key, bool down, bool modified, Point point, bool hasPoint = true)
    {
        var route = policy.Key(key, down, modified);
        if (route.Message == -1) Generation++;
        if (route.Message == 0x207 && !hasPoint) Cancel();
        else if (route.Message != 0 && (hasPoint || route.Message == -1)) Emit(route.Message, point);
        return route;
    }
    private void Emit(int message, Point point)
    {
        if (stopped) return;
        if (message == 0x207)
        {
            if (snapshot != null) { queue(snapshot(point) with { Message = message, Point = point, Generation = Generation }); return; }
            var window = Native.GetAncestor(Native.WindowFromPoint(point), 2);
            Native.GetWindowThreadProcessId(window, out var pid);
            queue(new(message, point, window, Native.Title(window), Screen.FromPoint(point).Bounds, pid == Environment.ProcessId, Generation));
        }
        else queue(new(message, point, Generation: Generation));
    }
    internal void Cancel() { Generation++; policy.Cancel(); }
    public void Dispose()
    {
        if (stopped) return;
        stopped = true; Generation++; policy.Stop();
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        mouseHook = 0;
        if (keyboardHook == 0 || !policy.NeedsRelease) { Unhook(); return; }
        // Retain only owned key pairs, at most five seconds. Timer roots this adapter
        // until release/timeout; no future presses are accepted during draining.
        var deadline = Environment.TickCount64 + 5000;
        drain = new System.Windows.Forms.Timer { Interval = 25 };
        drain.Tick += (_, _) => { if (!policy.NeedsRelease || Environment.TickCount64 >= deadline) Unhook(); };
        drain.Start();
    }
    private void Unhook()
    {
        drain?.Stop(); drain?.Dispose(); drain = null;
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
        mouseHook = keyboardHook = 0;
    }
}
