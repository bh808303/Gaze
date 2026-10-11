namespace Gaze.Core;

public enum SessionState { Idle, Armed, Recording, Paused, Saving }
public enum CaptureMode { Window, AllMonitors, CurrentMonitor }
public enum WindowCanvasMode { OriginalWindow, Monitor }

public sealed class Session
{
    public SessionState State { get; private set; }
    public nint Root { get; private set; }
    public CaptureMode Mode { get; private set; }
    public TimeSpan Duration { get; private set; }
    public void Arm(CaptureMode mode, nint root)
    {
        Require(SessionState.Idle);
        if (mode != CaptureMode.AllMonitors && root == 0) throw new ArgumentException("Select a window first.");
        Mode = mode; Root = root; Duration = TimeSpan.Zero; State = SessionState.Armed;
    }
    public void Start() { Require(SessionState.Armed); State = SessionState.Recording; }
    public void Pause() { Require(SessionState.Recording); State = SessionState.Paused; }
    public void Resume() { Require(SessionState.Paused); State = SessionState.Recording; }
    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (State == SessionState.Recording) Duration += elapsed;
    }
    public void Stop()
    {
        if (State is not (SessionState.Recording or SessionState.Paused)) throw new InvalidOperationException("No recording to stop.");
        State = SessionState.Saving;
    }
    public void Reset() { State = SessionState.Idle; Root = 0; Duration = TimeSpan.Zero; }
    private void Require(SessionState state)
    {
        if (State != state) throw new InvalidOperationException($"Expected {state}, currently {State}.");
    }
}

public sealed class EscapeConfirmation
{
    private TimeSpan? first;
    public bool Press(TimeSpan now, bool repeated = false)
    {
        if (repeated) return false;
        if (first.HasValue && now >= first.Value && now - first.Value <= TimeSpan.FromSeconds(1))
        {
            first = null; return true;
        }
        first = now; return false;
    }
    public void Reset() => first = null;
}
