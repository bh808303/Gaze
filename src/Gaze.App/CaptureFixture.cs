using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using Gaze.Core;

namespace Gaze.App;

// Records only synthetic fixture content unless --all-monitors is explicitly selected.
internal sealed class CaptureFixture : Form
{
    private readonly string evidence;
    private readonly string[] args;
    private readonly Stopwatch clock = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private Capture? capture;
    private Form? dialog, cover;
    private readonly Overlay overlay = new();
    private readonly List<string> events = [];
    private bool opened, closed, paused, resumed, stopping;
    private bool checkedDialogScale;
    private bool eventInputSent;
    private bool coverInputSent;
    private readonly ContextMenuStrip testMenu = new();
    private bool menuOpened, menuInputSent, menuItemActivated;
    private Point menuOpenPoint, menuItemPoint;
    private double menuShownAt;
    private readonly ComboBox testCombo = new() { Location = new Point(100, 400), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox editableCombo = new() { Location = new Point(400, 400), Width = 240, DropDownStyle = ComboBoxStyle.DropDown };
    private bool editableOpened, editableDown, editableUp;
    private double editableShownAt, editableDownAt;
    private Point editablePoint;
    private bool nativeMenuStarted, nativeMenuDown, nativeMenuUp, nativeMenuActivated, comboOpened, comboDown, comboUp;
    private double nativeMenuShownAt, nativeMenuDownAt, comboShownAt, comboDownAt;
    private Point nativeMenuPoint, comboPoint;
    private Point popupOriginalCursor;
    private bool syntheticButtonHeld;
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint owner, nint rectangle);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [StructLayout(LayoutKind.Sequential)] private struct ComboInfo { internal uint Size; internal RectangleItems Item, Button; internal uint State; internal nint Combo, Edit, List; }
    [StructLayout(LayoutKind.Sequential)] private struct RectangleItems { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(nint combo, ref ComboInfo info);
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct TestInput { [FieldOffset(0)] internal uint Type; [FieldOffset(8)] internal MouseInput Mouse; [FieldOffset(8)] internal KeyboardInput Keyboard; }
    private bool KeyboardCheck => args.Contains("--keyboard-check") || args.Contains("--keyboard-off-check");
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, TestInput[] inputs, int size);
    private double recordingStart;
    private nint current;
    internal CaptureFixture(string[] args)
    {
        this.args = args;
        var i = Array.IndexOf(args, "--evidence");
        evidence = i >= 0 ? Path.GetFullPath(args[i + 1]) : Path.GetFullPath("docs/factory/runs/prototype-001/runtime");
        Directory.CreateDirectory(evidence);
        Text = "Gaze synthetic capture fixture"; StartPosition = FormStartPosition.Manual;
        Location = new Point(80, 80); ClientSize = new Size(900, 560);
        BackColor = Color.FromArgb(240, 245, 250);
        var content = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16), ForeColor = Color.FromArgb(20, 45, 70), Text = "GAZE WINDOW CAPTURE TEST\n\nReadable text: abcdefghijklmnopqrstuvwxyz 0123456789\n\nInvoice 12345 · Status: Pending\n\nThis text must remain visible while covered by another window.\n\nDialog capture must return here after closing.\n\nNative resolution · 5 FPS · no audio", Padding = new Padding(24) };
        Controls.Add(content);
        testMenu.Items.Add("Synthetic menu action", null, (_, _) => menuItemActivated = true);
        content.ContextMenuStrip = testMenu;
        testCombo.Items.AddRange(new object[] { "First synthetic option", "Second synthetic option" }); testCombo.SelectedIndex = 0;
        Controls.Add(testCombo); testCombo.BringToFront();
        editableCombo.Items.AddRange(new object[] { "First editable option", "Second editable option" }); editableCombo.SelectedIndex = 0;
        Controls.Add(editableCombo); editableCombo.BringToFront();
        Shown += async (_, _) =>
        {
            Native.ShowWindow(Handle, 5);
            Show(); Activate(); Refresh();
            await Task.Delay(600);
            Begin();
        };
        timer.Tick += (_, _) => Step();
    }
    private void Log(string message) { events.Add($"{clock.Elapsed.TotalSeconds:F2}s {message}"); File.WriteAllLines(Path.Combine(evidence, "events.txt"), events); }
    private void Begin()
    {
        clock.Start(); current = Handle;
        Log($"Fixture HWND {Handle}, visible {Native.IsWindowVisible(Handle)}, minimized {Native.IsIconic(Handle)}");
        Log(Native.DesktopInfo());
        Log(Native.CaptureInfo(Handle));
        using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(evidence, "fixture-render.png")); }
        capture = new Capture();
        capture.StatusChanged += status => BeginInvoke(() =>
        {
            Log($"Recorder {status}");
            if (status == RecorderStatus.Recording && !timer.Enabled) { recordingStart = clock.Elapsed.TotalSeconds; timer.Start(); }
        });
        capture.Failed += error => BeginInvoke(() => Finish(false, error));
        capture.Completed += paths => BeginInvoke(() =>
        {
            if (args.Contains("--event-check"))
            {
                try { CheckEventMetadata(paths); }
                catch (Exception e) { Finish(false, e.ToString()); return; }
            }
            Gaze.App.Capture.CopyFiles(paths);
            bool copied = paths.All(path => Clipboard.GetFileDropList().Contains(path));
            Finish(copied, $"Output: {string.Join(", ", paths)}; clipboard: {copied}");
        });
        try
        {
            if (Native.CaptureTargetError(0, (uint)Environment.ProcessId) == null ||
                Native.CaptureTargetError(Handle, (uint)Environment.ProcessId) == null ||
                Native.CaptureTargetError(Handle, 0) != null)
                throw new InvalidOperationException("Capture target validation failed for absent, own, or application windows.");
            int rejectedShellWindows = 0;
            Native.EnumWindows((hwnd, _) =>
            {
                if (Native.CaptureTargetError(hwnd, 0)?.StartsWith("The taskbar and desktop") == true) rejectedShellWindows++;
                return true;
            }, 0);
            if (rejectedShellWindows == 0) throw new InvalidOperationException("No shell surfaces found to verify taskbar rejection.");
            Log($"Target validation passed; {rejectedShellWindows} shell surfaces rejected.");
            var canvasMode = args.Contains("--monitor-canvas") ? WindowCanvasMode.Monitor : WindowCanvasMode.OriginalWindow;
            var compression = args.Contains("--compact") ? CompressionMode.Compact : CompressionMode.Original;
            var persisted = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { WindowCanvas = canvasMode, Compression = compression, KeyboardTimeline = args.Contains("--keyboard-check") }))!;
            if (JsonSerializer.Deserialize<Settings>("{}")!.KeyboardTimeline || persisted.KeyboardTimeline != args.Contains("--keyboard-check")) throw new InvalidOperationException("Keyboard setting default or persistence failed.");
            if (persisted.Compression != compression || JsonSerializer.Deserialize<Settings>("{}")!.Compression != CompressionMode.Original)
                throw new InvalidOperationException("Compression settings did not round-trip or preserve the default.");
            if (persisted.WindowCanvas != canvasMode || JsonSerializer.Deserialize<Settings>("{}")!.WindowCanvas != WindowCanvasMode.OriginalWindow)
                throw new InvalidOperationException("Canvas settings did not round-trip or preserve the default.");
            capture.Start(args.Contains("--all-monitors") ? CaptureMode.AllMonitors : CaptureMode.Window, Handle, 5, !args.Contains("--no-click-highlight"), evidence, true, persisted.WindowCanvas, persisted.Compression, persisted.SplitAt29Mb, persisted.KeyboardTimeline);
            var expected = canvasMode == WindowCanvasMode.Monitor ? Screen.FromHandle(Handle).Bounds.Size : Size;
            if (Math.Abs(capture.Canvas.Width - expected.Width) > 24 || Math.Abs(capture.Canvas.Height - expected.Height) > 24)
                throw new InvalidOperationException("Canvas does not match the selected policy.");
            if (!args.Contains("--no-overlay")) { overlay.Location = new Point(Left + 100, Top + 100); overlay.Display("GAZE OVERLAY MUST NOT APPEAR"); }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Step()
    {
        try
        {
            double elapsed = clock.Elapsed.TotalSeconds - recordingStart;
            if (args.Contains("--event-check") && elapsed > 1 && !eventInputSent)
            {
                eventInputSent = true; Text = "Gaze synthetic fixture — Invoice 4242";
                TopMost = true; BringToFront(); Activate();
                Native.GetCursorPos(out var original);
                var point = PointToScreen(new Point(80, 100));
                Native.GetWindowThreadProcessId(Native.WindowFromPoint(point), out var clickProcess);
                if (clickProcess != (uint)Environment.ProcessId) throw new InvalidOperationException("Fixture click point is covered; no test clicks were injected.");
                SetCursorPos(point.X, point.Y);
                var inputs = Enumerable.Range(0, 10).Select(i => new TestInput { Type = 0, Mouse = new MouseInput { Flags = i % 2 == 0 ? 2u : 4u } }).ToArray();
                try { if (SendInput(10, inputs, Marshal.SizeOf<TestInput>()) != 10) throw new InvalidOperationException("Could not inject the synthetic click test."); }
                finally { SetCursorPos(original.X, original.Y); }
                Log("Five rapid synthetic clicks sent between frames; fixture title changed.");
                if (KeyboardCheck) InjectKeys(Handle, (117, false), (117, false), (117, true), (160, false), (65, false), (65, true), (160, true));
            }
            if (elapsed > 2 && cover == null)
            {
                TopMost = false;
                cover = new Form { Text = "Unrelated covering window", TopMost = args.Contains("--event-check"), StartPosition = FormStartPosition.Manual, Location = Location, Size = Size, BackColor = Color.Magenta };
                cover.Controls.Add(new Label { Text = "UNRELATED WINDOW MUST NOT APPEAR IN WINDOW CAPTURE", Dock = DockStyle.Fill }); cover.Show(); Log("Root covered by unrelated window");
            }
            if (elapsed > 4 && !opened)
            {
                opened = true;
                dialog = new Form { Text = "Owned fixture dialog", TopMost = args.Contains("--event-check"), StartPosition = FormStartPosition.Manual, Location = new Point(Left + 60, Top + 60), ClientSize = args.Contains("--large-dialog") ? new Size(1200, 800) : new Size(500, 300), BackColor = Color.FromArgb(215, 245, 220) };
                dialog.Controls.Add(new Label { Text = "OWNED DIALOG\n\nExpected captured text: Dialog 67890\n\nWindow switches temporarily.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16), Padding = new Padding(24) });
                dialog.Show(this); Log("Owned dialog opened");
            }
            if (args.Contains("--event-check") && !args.Contains("--all-monitors") && elapsed > 3 && !coverInputSent && cover != null)
            {
                cover.BringToFront(); cover.Activate();
                var point = cover.PointToScreen(new Point(80, 100));
                if (Native.GetAncestor(Native.WindowFromPoint(point), 2) != cover.Handle)
                    throw new InvalidOperationException("Synthetic covering window is not under the click point; no click injected.");
                Native.GetCursorPos(out var original); SetCursorPos(point.X, point.Y);
                var inputs = new[] { new TestInput { Type = 0, Mouse = new MouseInput { Flags = 2 } }, new TestInput { Type = 0, Mouse = new MouseInput { Flags = 4 } } };
                try { if (SendInput(2, inputs, Marshal.SizeOf<TestInput>()) != 2) throw new InvalidOperationException("Could not inject the covering-window click."); }
                finally { SetCursorPos(original.X, original.Y); }
                coverInputSent = true; Log("Synthetic click sent to unrelated covering window; it must not enter the timeline.");
                if (KeyboardCheck) InjectKeys(cover.Handle, (118, false), (118, true));
            }
            if (elapsed > 7 && !closed) { closed = true; dialog?.Close(); cover?.Close(); Log("Dialog and covering window closed"); }
            if (args.Contains("--event-check") && !args.Contains("--all-monitors") && elapsed > 7.2 && !menuOpened)
            {
                TopMost = true; BringToFront(); Activate();
                menuOpenPoint = PointToScreen(new Point(350, 200));
                InjectClick(menuOpenPoint, true, Handle);
                menuOpened = true; menuShownAt = elapsed;
                Log("Right-click sent to recorded fixture to open its context menu.");
            }
            if (menuOpened && !menuInputSent && elapsed > menuShownAt + 0.35)
            {
                if (!testMenu.Visible) throw new InvalidOperationException("Right-click did not open the fixture context menu.");
                var item = testMenu.Items[0].Bounds;
                menuItemPoint = testMenu.PointToScreen(new Point(item.Left + item.Width / 2, item.Top + item.Height / 2));
                Log($"Context menu HWND {testMenu.Handle}; owner recognized: {Native.OwnedBy(testMenu.Handle, Handle)}");
                InjectClick(menuItemPoint, false, testMenu.Handle);
                menuInputSent = true;
                Log("Left-click sent to the synthetic context-menu item.");
            }
            if (elapsed > 6 && !checkedDialogScale && args.Contains("--large-dialog"))
            {
                checkedDialogScale = true;
                var expectedScaled = args.Contains("--monitor-canvas")
                    ? dialog!.Width > capture!.Canvas.Width || dialog.Height > capture.Canvas.Height
                    : true;
                if (capture!.IsScaled != expectedScaled) throw new InvalidOperationException("Larger dialog scaling does not match the fixed canvas.");
                Log($"Large dialog scaling verified: {capture.IsScaled}");
            }
            if (!args.Contains("--all-monitors"))
            {
                var next = Native.FollowedWindow(Handle, current);
                if (next != current) { capture!.Switch(next); current = next; Log($"Switched to {Native.Title(next)}"); }
            }
            if (elapsed > 8 && !paused) { paused = true; capture!.Pause(); Log("Paused"); if (KeyboardCheck) InjectKeys(Handle, (120, false), (120, true)); }
            if (elapsed > 9.5 && !resumed) { resumed = true; capture!.Resume(); Log("Resumed"); if (KeyboardCheck) InjectKeys(Handle, (122, false), (122, true)); }
            if (args.Contains("--event-check") && !args.Contains("--all-monitors") && resumed && elapsed > 10.3 && !nativeMenuStarted)
            {
                nativeMenuStarted = true; nativeMenuShownAt = elapsed;
                var menu = CreatePopupMenu();
                if (menu == 0 || !AppendMenu(menu, 0, 101, "Native synthetic menu action")) throw new InvalidOperationException("Cannot create native test menu.");
                var point = PointToScreen(new Point(350, 260));
                try { nativeMenuActivated = TrackPopupMenu(menu, 0x100, point.X, point.Y, 0, Handle, 0) == 101; }
                finally { DestroyMenu(menu); }
            }
            if (nativeMenuStarted && !nativeMenuDown && elapsed > nativeMenuShownAt + 0.35)
            {
                nint popup = 0;
                Native.EnumWindows((hwnd, _) =>
                {
                    Native.GetWindowThreadProcessId(hwnd, out var pid);
                    if (pid == (uint)Environment.ProcessId && Native.IsWindowVisible(hwnd) && Native.ClassName(hwnd) == "#32768") { popup = hwnd; return false; }
                    return true;
                }, 0);
                if (popup == 0) throw new InvalidOperationException("Native #32768 menu was not found.");
                var bounds = Native.WindowBounds(popup); nativeMenuPoint = new Point(bounds.Left + 40, bounds.Top + bounds.Height / 2);
                Log($"Native menu class {Native.ClassName(popup)}; owner recognized: {Native.OwnedBy(popup, Handle)}");
                InjectButton(nativeMenuPoint, true, popup); nativeMenuDown = true; nativeMenuDownAt = elapsed;
            }
            if (nativeMenuDown && !nativeMenuUp && elapsed > nativeMenuDownAt + 0.4)
            { InjectButton(nativeMenuPoint, false, 0); nativeMenuUp = true; }
            if (nativeMenuUp && nativeMenuActivated && !comboOpened && elapsed > 11.5)
            { testCombo.Focus(); testCombo.DroppedDown = true; comboOpened = true; comboShownAt = elapsed; }
            if (comboOpened && !comboDown && elapsed > comboShownAt + 0.35)
            {
                var info = new ComboInfo { Size = (uint)Marshal.SizeOf<ComboInfo>() };
                if (!GetComboBoxInfo(testCombo.Handle, ref info) || !Native.IsWindowVisible(info.List)) throw new InvalidOperationException("Combo dropdown was not found.");
                var bounds = Native.WindowBounds(info.List); comboPoint = new Point(bounds.Left + 40, bounds.Top + testCombo.ItemHeight * 3 / 2 + 2);
                Log($"Combo popup class {Native.ClassName(info.List)}; owner recognized: {Native.OwnedBy(info.List, Handle)}");
                InjectButton(comboPoint, true, info.List); comboDown = true; comboDownAt = elapsed;
            }
            if (comboDown && !comboUp && elapsed > comboDownAt + 0.4)
            { InjectButton(comboPoint, false, 0); comboUp = true; }
            if (comboUp && !editableOpened && elapsed > 13)
            { editableCombo.Focus(); editableCombo.DroppedDown = true; editableOpened = true; editableShownAt = elapsed; }
            if (editableOpened && !editableDown && elapsed > editableShownAt + 0.35)
            {
                var info = new ComboInfo { Size = (uint)Marshal.SizeOf<ComboInfo>() };
                if (!GetComboBoxInfo(editableCombo.Handle, ref info) || !Native.IsWindowVisible(info.List)) throw new InvalidOperationException("Editable combo dropdown was not found.");
                var bounds = Native.WindowBounds(info.List); editablePoint = new Point(bounds.Left + 40, bounds.Top + editableCombo.ItemHeight * 3 / 2 + 2);
                var focus = GetFocus(); var focusedInfo = new ComboInfo { Size = (uint)Marshal.SizeOf<ComboInfo>() };
                if (focus != info.Edit || Native.GetParent(focus) != editableCombo.Handle || GetComboBoxInfo(focus, ref focusedInfo))
                    throw new InvalidOperationException("Editable combo regression did not exercise the focused Edit-child fallback.");
                Log($"Editable combo popup class {Native.ClassName(info.List)}; owner recognized: {Native.OwnedBy(info.List, Handle)}; focused child class: {Native.ClassName(focus)}; direct combo lookup failed as expected.");
                InjectButton(editablePoint, true, info.List); editableDown = true; editableDownAt = elapsed;
            }
            if (editableDown && !editableUp && elapsed > editableDownAt + 0.4)
            { InjectButton(editablePoint, false, 0); editableUp = true; }
            if (elapsed > 16 && !stopping)
            {
                stopping = true;
                Log("Stopping"); capture!.Stop();
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void InjectClick(Point point, bool right, nint expectedWindow)
    {
        if (Native.GetAncestor(Native.WindowFromPoint(point), 2) != expectedWindow)
            throw new InvalidOperationException("Synthetic click target is covered; no click injected.");
        Native.GetCursorPos(out var original); SetCursorPos(point.X, point.Y);
        var inputs = new[] { new TestInput { Type = 0, Mouse = new MouseInput { Flags = right ? 8u : 2u } }, new TestInput { Type = 0, Mouse = new MouseInput { Flags = right ? 16u : 4u } } };
        try { if (SendInput(2, inputs, Marshal.SizeOf<TestInput>()) != 2) throw new InvalidOperationException("Could not inject synthetic menu click."); }
        finally { SetCursorPos(original.X, original.Y); }
    }
    private void InjectKeys(nint expectedWindow, params (ushort Key, bool Up)[] keys)
    {
        if (Native.GetForegroundWindow() != expectedWindow) throw new InvalidOperationException("Synthetic keyboard target is not foreground; no keys injected.");
        var inputs = keys.Select(key => new TestInput { Type = 1, Keyboard = new KeyboardInput { Key = key.Key, Flags = key.Up ? 2u : 0u } }).ToArray();
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<TestInput>()) != inputs.Length) throw new InvalidOperationException("Synthetic key injection failed.");
        Log($"Sent {inputs.Length} synthetic key transitions to guarded fixture window.");
    }
    private void InjectButton(Point point, bool down, nint expectedWindow)
    {
        var hit = Native.WindowFromPoint(point); Native.GetWindowThreadProcessId(hit, out var pid);
        if (pid != (uint)Environment.ProcessId || (expectedWindow != 0 && Native.GetAncestor(hit, 2) != Native.GetAncestor(expectedWindow, 2)))
            throw new InvalidOperationException("Synthetic popup button target is covered; no input injected.");
        if (down) Native.GetCursorPos(out popupOriginalCursor);
        SetCursorPos(point.X, point.Y);
        var inputs = new[] { new TestInput { Type = 0, Mouse = new MouseInput { Flags = down ? 2u : 4u } } };
        if (SendInput(1, inputs, Marshal.SizeOf<TestInput>()) != 1) throw new InvalidOperationException("Synthetic popup input failed.");
        syntheticButtonHeld = down;
        if (!down) SetCursorPos(popupOriginalCursor.X, popupOriginalCursor.Y);
    }
    private void CheckEventMetadata(IReadOnlyList<string> paths)
    {
        var allEvents = new List<JsonElement>(); var anchors = new List<JsonElement>(); int chapterCount = 0;
        foreach (var path in paths)
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GAZE_TEST_PYTHON") ?? "python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "Read-Timeline.py"), path, "--probe", Path.Combine(AppContext.BaseDirectory, "ffprobe.exe") }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) { process.Kill(); throw new InvalidOperationException("Timeline reader timed out."); }
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult());
            string json = output.GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(evidence, Path.GetFileNameWithoutExtension(path) + ".timeline.json"), json);
            using var data = JsonDocument.Parse(json);
            allEvents.AddRange(data.RootElement.GetProperty("events").EnumerateArray().Select(e => e.Clone()));
            anchors.AddRange(data.RootElement.GetProperty("clockAnchors").EnumerateArray().Select(e => e.Clone()));
            chapterCount += data.RootElement.GetProperty("chapters").GetArrayLength();
        }
        var clicks = allEvents.Where(e => e.GetProperty("kind").GetString() == "click").ToArray();
        if (KeyboardCheck)
        {
            var keys = allEvents.Where(e => e.GetProperty("kind").GetString() == "key").ToArray();
            int expected = args.Contains("--keyboard-check") ? 9 : 0;
            if (keys.Length != expected || keys.Any(e => e.GetProperty("virtualKey").GetInt32() is 118 or 120)) throw new InvalidOperationException($"Keyboard default/boundary/pause check failed: expected {expected}, got {keys.Length}.");
            if (expected > 0 && (!keys.Any(e => e.GetProperty("repeat").GetBoolean()) || keys.Count(e => e.GetProperty("virtualKey").GetInt32() == 122) != 2 || keys.Any(e => !e.GetProperty("injected").GetBoolean()))) throw new InvalidOperationException("Keyboard repeat/resume/injected fields missing.");
            Log($"PASS: {keys.Length} key events; foreign and paused keys absent, setting respected.");
        }
        if (clicks.Length < 5 || clicks.GroupBy(e => e.GetProperty("frame").GetInt32()).Max(g => g.Count()) < 4)
            throw new InvalidOperationException("Rapid clicks between video frames were not preserved.");
        if (!allEvents.Any(e => e.GetProperty("kind").GetString() == "window" && e.TryGetProperty("title", out var title) && title.GetString()!.Contains("Invoice 4242")) || chapterCount < 2)
            throw new InvalidOperationException("Actual window title changes or chapters are missing.");
        if (!args.Contains("--all-monitors"))
        {
            if (!coverInputSent || clicks.Length != 10) throw new InvalidOperationException($"Expected five rapid clicks plus five popup clicks, got {clicks.Length}; covering-window click filtering or popup retention failed.");
            bool HasClick(Point point, string button) => clicks.Any(e => e.GetProperty("button").GetString() == button && e.GetProperty("screenX").GetInt32() == point.X && e.GetProperty("screenY").GetInt32() == point.Y);
            if (!menuInputSent || !menuItemActivated || !HasClick(menuOpenPoint, "right") || !HasClick(menuItemPoint, "left"))
                throw new InvalidOperationException("Context-menu opening or item click was lost.");
            Log("PASS: recorded-app context-menu opening and item clicks retained; menu action executed.");
            if (!nativeMenuActivated || !nativeMenuUp || !HasClick(nativeMenuPoint, "left") || !comboUp || testCombo.SelectedIndex != 1 || !HasClick(comboPoint, "left"))
                throw new InvalidOperationException("Native menu or combo-list click/action was lost.");
            Log("PASS: native #32768 menu and ComboLBox item clicks retained while popups stayed open across frame ticks; both actions executed.");
            if (!editableUp || editableCombo.SelectedIndex != 1 || !HasClick(editablePoint, "left"))
                throw new InvalidOperationException("Editable combo dropdown click or selection was lost.");
            Log("PASS: editable DropDown combo item click retained with its Edit child focused; selection executed.");
            var external = allEvents.Where(e => e.TryGetProperty("external", out var flag) && flag.GetBoolean()).ToArray();
            if (external.Length == 0 || external.Any(e => e.TryGetProperty("title", out _) || e.TryGetProperty("process", out _) || e.TryGetProperty("windowId", out _)))
                throw new InvalidOperationException("Foreign foreground context was not anonymized.");
            if (allEvents.Any(e => e.TryGetProperty("title", out var title) && title.GetString() == "Unrelated covering window"))
                throw new InvalidOperationException("The unrelated covering window's title leaked.");
            if (!allEvents.Any(e => e.TryGetProperty("title", out var title) && title.GetString() == "Owned fixture dialog"))
                throw new InvalidOperationException("Captured owned dialog title was lost.");
            Log("PASS: external foreground events have no identity; captured owned dialog retains its title.");
        }
        if (!anchors.Any(a => a.GetProperty("reason").GetString() == "resume") || !allEvents.Any(e => e.GetProperty("kind").GetString() == "pause"))
            throw new InvalidOperationException("Pause/resume clock mapping is missing.");
        Log($"PASS: {clicks.Length} independent clicks, {chapterCount} chapters, resume clock anchor; event track extracted from actual recording.");
    }
    private void Finish(bool success, string detail)
    {
        if (syntheticButtonHeld)
        {
            SendInput(1, new[] { new TestInput { Type = 0, Mouse = new MouseInput { Flags = 4 } } }, Marshal.SizeOf<TestInput>());
            syntheticButtonHeld = false; SetCursorPos(popupOriginalCursor.X, popupOriginalCursor.Y);
        }
        timer.Stop(); testMenu.Close(); overlay.Close(); dialog?.Close(); cover?.Close();
        Log(detail);
        File.WriteAllText(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new { success, detail, canvas = capture?.Canvas, events }, new JsonSerializerOptions { WriteIndented = true }));
        capture?.Dispose(); Environment.ExitCode = success ? 0 : 1; Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); testMenu.Dispose(); overlay.Dispose(); capture?.Dispose(); }
        base.Dispose(disposing);
    }
}
