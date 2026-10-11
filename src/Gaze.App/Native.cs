using System.Runtime.InteropServices;
using System.Text;

namespace Gaze.App;

internal static class Native
{
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int length);
    internal static nint MonitorHandle(Screen screen) => MonitorFromPoint(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2);
    internal static string? CaptureTargetError(nint hwnd, uint excludedProcess)
    {
        if (hwnd == 0 || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return "Focus the application window you want to record, then arm Gaze.";
        var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
        if (hwnd == GetDesktopWindow() || hwnd == GetShellWindow() || name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW" or "#32769")
            return "The taskbar and desktop cannot be selected as a window. Click inside your application and arm Gaze again, or choose All monitors.";
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == excludedProcess) return "Focus the application you want to record instead of Gaze, then arm again.";
        if (IsIconic(hwnd)) return "Restore the application window before arming Gaze.";
        var bounds = Bounds(hwnd);
        if (bounds.Width < 2 || bounds.Height < 2) return "The selected window has no visible capture area.";
        return null;
    }
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
    internal static void RefreshWindow(nint hwnd) => RedrawWindow(hwnd, 0, 0, 0x1 | 0x80 | 0x400);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    internal static Rectangle WindowBounds(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect)) throw new InvalidOperationException("Cannot read Gaze panel bounds.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out Rect rect, int size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumCallback callback, nint data);
    internal delegate bool EnumCallback(nint hwnd, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    internal static string Title(nint hwnd)
    {
        var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity); return text.ToString();
    }
    internal static Rectangle Bounds(nint hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, 9, out Rect rect, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(hwnd, out rect))
            throw new InvalidOperationException("Cannot read selected window dimensions.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
    internal static bool OwnedBy(nint hwnd, nint root)
    {
        for (int i = 0; hwnd != 0 && i < 64; i++)
        {
            if (hwnd == root) return true;
            hwnd = GetWindow(hwnd, 4);
        }
        return false;
    }
    internal static nint FollowedWindow(nint root, nint current)
    {
        var foreground = GetForegroundWindow();
        if (IsWindowVisible(foreground) && OwnedBy(foreground, root)) return foreground;
        if (current != root && IsWindow(current) && IsWindowVisible(current)) return current;
        nint candidate = root;
        EnumWindows((hwnd, _) =>
        {
            if (hwnd != root && IsWindowVisible(hwnd) && !IsIconic(hwnd) && OwnedBy(hwnd, root))
            { candidate = hwnd; return false; }
            return true;
        }, 0);
        return candidate;
    }

    internal delegate nint HookCallback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int id, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll")] private static extern nint GetProcessWindowStation();
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out uint value, int size);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
    internal static string CaptureInfo(nint hwnd)
    {
        DwmGetWindowAttribute(hwnd, 14, out uint cloaked, 4); GetWindowDisplayAffinity(hwnd, out var affinity);
        return $"Cloaked: {cloaked}; affinity: {affinity}";
    }
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder info, uint size, out uint needed);
    internal static string DesktopInfo()
    {
        var text = new StringBuilder(256); GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, text, 512, out _);
        var input = OpenInputDesktop(0, false, 1); var inputName = new StringBuilder(256);
        if (input != 0) { GetUserObjectInformation(input, 2, inputName, 512, out _); CloseDesktop(input); }
        var station = new StringBuilder(256); GetUserObjectInformation(GetProcessWindowStation(), 2, station, 512, out _);
        DwmIsCompositionEnabled(out var composition);
        return $"Station: {station}; Thread desktop: {text}; input desktop: {inputName}; DWM: {composition}";
    }
}
