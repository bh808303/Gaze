using Gaze.Core;

var failures = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
Test("Recording names include date, start/end, and video minutes across midnight", () =>
{
    var start = new DateTime(2026, 10, 10, 23, 59, 0);
    Equal("Gaze_d101026_t2359-0001_2min.mp4", RecordingName.Create(start, start.AddMinutes(2.5), TimeSpan.FromMinutes(2.5)));
    Equal("Gaze_d101026_t2359-0001_1min.mp4", RecordingName.Create(start, start.AddMinutes(2.5), TimeSpan.FromMinutes(1)));
    Equal("Gaze_d101026_t2359-2359_0min.mp4", RecordingName.Create(start, start.AddSeconds(30), TimeSpan.FromSeconds(30)));
    Equal(false, RecordingName.Create(start, start, TimeSpan.Zero).Contains(':'));
});
Test("Arming locks the original target", () =>
{
    var s = new Session(); s.Arm(CaptureMode.Window, 42); s.Start();
    Equal((nint)42, s.Root); Equal(SessionState.Recording, s.State);
});
Test("Current monitor locks its armed target and requires a monitor", () =>
{
    var s = new Session(); s.Arm(CaptureMode.CurrentMonitor, 77); s.Start();
    Equal((nint)77, s.Root); Equal(CaptureMode.CurrentMonitor, s.Mode);
    s.Stop(); Equal(SessionState.Saving, s.State);
    try { new Session().Arm(CaptureMode.CurrentMonitor, 0); throw new Exception("Missing monitor was accepted"); }
    catch (ArgumentException) { }
    Equal(1, (int)CaptureMode.AllMonitors);
});
Test("Pause excludes time from video duration", () =>
{
    var s = new Session(); s.Arm(CaptureMode.Window, 42); s.Start();
    s.Tick(TimeSpan.FromSeconds(3)); s.Pause(); s.Tick(TimeSpan.FromSeconds(20));
    s.Resume(); s.Tick(TimeSpan.FromSeconds(2)); Equal(TimeSpan.FromSeconds(5), s.Duration);
});
Test("Saving blocks rearming until completion", () =>
{
    var s = new Session(); s.Arm(CaptureMode.AllMonitors, 0); s.Start(); s.Stop();
    Equal(SessionState.Saving, s.State);
    try { s.Arm(CaptureMode.Window, 99); throw new Exception("Rearm was accepted"); }
    catch (InvalidOperationException) { }
    s.Reset(); Equal(SessionState.Idle, s.State);
});
Test("Window mode requires a target", () =>
{
    try { new Session().Arm(CaptureMode.Window, 0); throw new Exception("Missing window was accepted"); }
    catch (ArgumentException) { }
});
Test("Esc stops only on a separate press within one second", () =>
{
    var e = new EscapeConfirmation(); Equal(false, e.Press(TimeSpan.Zero));
    Equal(false, e.Press(TimeSpan.FromMilliseconds(100), true));
    Equal(true, e.Press(TimeSpan.FromMilliseconds(900)));
});
Test("Expired Esc starts a new confirmation interval", () =>
{
    var e = new EscapeConfirmation(); e.Press(TimeSpan.Zero);
    Equal(false, e.Press(TimeSpan.FromSeconds(1.1)));
    Equal(true, e.Press(TimeSpan.FromSeconds(1.5)));
});
Test("Reset clears an earlier Esc confirmation", () =>
{
    var e = new EscapeConfirmation(); e.Press(TimeSpan.Zero); e.Reset();
    Equal(false, e.Press(TimeSpan.FromMilliseconds(500)));
});
Environment.ExitCode = failures == 0 ? 0 : 1;
