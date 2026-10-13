using System.Diagnostics;
using System.Runtime.InteropServices;
using Gaze.Core;

namespace Gaze.App;

// Created and disposed on the capture owner's UI thread, which pumps hook events.
// Independent of frame rendering and the Highlight clicks setting.
internal sealed class InputEvents : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct MouseData
    {
        internal Point Point; internal uint Buttons, Flags, Time; internal nuint ExtraInfo;
    }
    private readonly Native.HookCallback mouseCallback;
    private readonly Native.HookCallback keyboardCallback;
    private nint keyboardHook;
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { internal uint VirtualKey, ScanCode, Flags, Time; internal nuint ExtraInfo; }
    private readonly Native.WinEventCallback windowCallback;
    private nint mouseHook, foregroundHook, titleHook;
    private bool warned;
    private readonly Action<nint, DateTimeOffset, string> describeWindow;
    private readonly MonitorBoundaryCache boundaryCache = new();
    internal InputEvents(Action<string, Point, DateTimeOffset> click, Action<string?, string?, string?, DateTimeOffset, string, bool> window, Action<string> warning, Func<nint> capturedWindow, CaptureMode mode, nint recordingRoot, IReadOnlyList<Rectangle> areas, Action<RawKey>? key = null, Action<RawMouse>? mouseEvent = null)
    {
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { if (!warned) { warned = true; warning($"Some input events could not be recorded: {e.Message}"); } }
        }
        void Window(nint hwnd, DateTimeOffset time, string scope, bool? boundary = null)
        {
            if (hwnd == 0 || !Native.IsWindow(hwnd)) return;
            hwnd = Native.GetAncestor(hwnd, 2); // GA_ROOT normalizes child notifications.
            bool related = mode == CaptureMode.Window && (hwnd == capturedWindow() || Native.OwnedBy(hwnd, recordingRoot));
            // Check the boundary before reading any title or process name.
            bool included = boundary ?? TimelineBoundary.IncludesWindow(mode, related,
                mode == CaptureMode.Window ? Rectangle.Empty : Native.Bounds(hwnd), areas);
            if (!included)
            {
                if (scope == "foreground") window(null, null, null, time, scope, true);
                if (scope == "foreground" && mode != CaptureMode.Window) boundaryCache.Remember(hwnd, false);
                return;
            }
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            string name = $"pid-{pid}";
            try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
            catch (ArgumentException) { } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
            window(name, Native.Title(hwnd), $"0x{hwnd:X}", time, scope, false);
            if (scope == "foreground" && mode != CaptureMode.Window) boundaryCache.Remember(hwnd, true);
        }
        describeWindow = (hwnd, time, scope) => Window(hwnd, time, scope);
        mouseCallback = (code, message, data) =>
        {
            if (code >= 0 && message is 0x200 or 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20B or 0x20C or 0x20A or 0x20E)
                Guard(() =>
                {
                    bool down = message is 0x201 or 0x204 or 0x207 or 0x20B;
                    if (mouseEvent == null && !down) return;
                    var mouse = Marshal.PtrToStructure<MouseData>(data);
                    string? button = message is 0x201 or 0x202 ? "left" : message is 0x204 or 0x205 ? "right" : message is 0x207 or 0x208 ? "middle" : message is 0x20B or 0x20C ? ((mouse.Buttons >> 16) == 1 ? "x1" : "x2") : null;
                    var time = ClockTime(mouse.Time);
                    if (mouseEvent != null)
                    {
                        string kind = down ? "click" : message == 0x200 ? "pointer" : message is 0x20A or 0x20E ? "wheel" : "button-release";
                        mouseEvent(new(kind, button, mouse.Point, time, Environment.TickCount64,
                            message is 0x20A or 0x20E ? unchecked((short)(mouse.Buttons >> 16)) : 0,
                            message == 0x20A ? "vertical" : message == 0x20E ? "horizontal" : null));
                    }
                    else click(button!, mouse.Point, time);
                });
            return Native.CallNextHookEx(mouseHook, code, message, data);
        };
        windowCallback = (_, type, hwnd, objectId, childId, _, time) =>
        {
            if (type == 0x800B)
            {
                if (mode == CaptureMode.Window || objectId != 0 || childId != 0) return;
                var movingRoot = Native.GetAncestor(hwnd, 2);
                if (!TimelineBoundary.HandlesLocation(mode, hwnd, movingRoot, Native.GetForegroundWindow(), objectId, childId)) return;
                Guard(() =>
                {
                    bool included = TimelineBoundary.IncludesWindow(mode, false, Native.Bounds(movingRoot), areas);
                    if (boundaryCache.Changed(movingRoot, included)) Window(movingRoot, ClockTime(time), "foreground", included);
                });
                return;
            }
            var root = Native.GetAncestor(hwnd, 2);
            if (type == 3 || (objectId == 0 && childId == 0 && root == Native.GetForegroundWindow()))
                Guard(() => Window(hwnd, ClockTime(time), "foreground"));
            if (type == 0x800C && objectId == 0 && childId == 0 && root == capturedWindow())
                Guard(() => Window(root, ClockTime(time), "captured"));
        };
        keyboardCallback = (code, message, data) =>
        {
            if (code >= 0 && message is 0x100 or 0x101 or 0x104 or 0x105)
                Guard(() =>
                {
                    var input = Marshal.PtrToStructure<KeyboardData>(data);
                    key?.Invoke(new((int)input.VirtualKey, (int)input.ScanCode, input.Flags, message is 0x100 or 0x104, Native.GetForegroundWindow(), ClockTime(input.Time)));
                });
            return Native.CallNextHookEx(keyboardHook, code, message, data);
        };
        try
        {
            mouseHook = Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
            if (key != null) keyboardHook = Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
            foregroundHook = Native.SetWinEventHook(3, 3, 0, windowCallback, 0, 0, 0);
            titleHook = Native.SetWinEventHook(mode == CaptureMode.Window ? 0x800Cu : 0x800Bu, 0x800C, 0, windowCallback, 0, 0, 0);
            if (mouseHook == 0 || foregroundHook == 0 || titleHook == 0 || (key != null && keyboardHook == 0))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not install recording event hooks.");
            Window(Native.GetForegroundWindow(), DateTimeOffset.Now, "foreground");
            Window(capturedWindow(), DateTimeOffset.Now, "captured");
        }
        catch { Dispose(); throw; }
    }
    internal void CapturedWindowChanged(nint hwnd) => describeWindow(hwnd, DateTimeOffset.Now, "captured");
    private static DateTimeOffset ClockTime(uint ticks)
    {
        // Hook timestamps use the wrapping Windows millisecond clock. Correct for
        // callback dispatch latency while retaining the actual local UTC offset.
        uint delay = unchecked((uint)Environment.TickCount - ticks);
        return DateTimeOffset.Now.AddMilliseconds(-(delay < 60_000 ? delay : 0));
    }
    public void Dispose()
    {
        if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
        keyboardHook = 0;
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (foregroundHook != 0) Native.UnhookWinEvent(foregroundHook);
        if (titleHook != 0) Native.UnhookWinEvent(titleHook);
        mouseHook = foregroundHook = titleHook = 0;
    }
}
