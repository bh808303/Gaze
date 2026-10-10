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
Test("Arming locks the original target", () =>
{
    var s = new Session(); s.Arm(CaptureMode.Window, 42); s.Start();
    Equal((nint)42, s.Root); Equal(SessionState.Recording, s.State);
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
