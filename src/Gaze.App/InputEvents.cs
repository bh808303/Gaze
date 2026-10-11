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
    private readonly Native.WinEventCallback windowCallback;
    private nint mouseHook, foregroundHook, titleHook;
    private bool warned;
    private readonly Action<nint, DateTimeOffset, string> describeWindow;
    internal InputEvents(Action<string, Point, DateTimeOffset> click, Action<string?, string?, string?, DateTimeOffset, string, bool> window, Action<string> warning, Func<nint> capturedWindow, CaptureMode mode, nint recordingRoot, IReadOnlyList<Rectangle> areas)
    {
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { if (!warned) { warned = true; warning($"Some input events could not be recorded: {e.Message}"); } }
        }
        void Window(nint hwnd, DateTimeOffset time, string scope)
        {
            if (hwnd == 0 || !Native.IsWindow(hwnd)) return;
            hwnd = Native.GetAncestor(hwnd, 2); // GA_ROOT normalizes child notifications.
            bool related = hwnd == capturedWindow() || Native.OwnedBy(hwnd, recordingRoot);
            // Check the boundary before reading any title or process name.
            bool included = TimelineBoundary.IncludesWindow(mode, related,
                mode == CaptureMode.Window ? Rectangle.Empty : Native.Bounds(hwnd), areas);
            if (!included)
            {
                if (scope == "foreground") window(null, null, null, time, scope, true);
                return;
            }
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            string name = $"pid-{pid}";
            try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
            catch (ArgumentException) { } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
            window(name, Native.Title(hwnd), $"0x{hwnd:X}", time, scope, false);
        }
        describeWindow = Window;
        mouseCallback = (code, message, data) =>
        {
            if (code >= 0 && message is 0x201 or 0x204 or 0x207 or 0x20B)
                Guard(() =>
                {
                    var mouse = Marshal.PtrToStructure<MouseData>(data);
                    string button = message == 0x201 ? "left" : message == 0x204 ? "right" : message == 0x207 ? "middle" : (mouse.Buttons >> 16) == 1 ? "x1" : "x2";
                    click(button, mouse.Point, ClockTime(mouse.Time));
                });
            return Native.CallNextHookEx(mouseHook, code, message, data);
        };
        windowCallback = (_, type, hwnd, objectId, childId, _, time) =>
        {
            var root = Native.GetAncestor(hwnd, 2);
            if (type == 3 || (objectId == 0 && childId == 0 && root == Native.GetForegroundWindow()))
                Guard(() => Window(hwnd, ClockTime(time), "foreground"));
            if (type == 0x800C && objectId == 0 && childId == 0 && root == capturedWindow())
                Guard(() => Window(root, ClockTime(time), "captured"));
        };
        try
        {
            mouseHook = Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
            foregroundHook = Native.SetWinEventHook(3, 3, 0, windowCallback, 0, 0, 0);
            titleHook = Native.SetWinEventHook(0x800B, 0x800C, 0, windowCallback, 0, 0, 0);
            if (mouseHook == 0 || foregroundHook == 0 || titleHook == 0)
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
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (foregroundHook != 0) Native.UnhookWinEvent(foregroundHook);
        if (titleHook != 0) Native.UnhookWinEvent(titleHook);
        mouseHook = foregroundHook = titleHook = 0;
    }
}
