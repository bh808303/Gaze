namespace Gaze.Core;

public enum TranscribeTrigger { MiddleMouse = 0, PrintScreen = 1 }
public readonly record struct TranscribeRoute(bool Consume, int Message = 0);

// Pure physical-pair ownership. Cancellation discards capture, not its owned release.
public sealed class TranscribeTriggerPolicy
{
    private readonly TranscribeTrigger trigger;
    private bool active = true, held, owned, canceled, escapeOwned;
    public TranscribeTriggerPolicy(TranscribeTrigger trigger = TranscribeTrigger.MiddleMouse, bool printScreenHeld = false)
    {
        if (!Enum.IsDefined(trigger)) throw new ArgumentOutOfRangeException(nameof(trigger));
        this.trigger = trigger; held = printScreenHeld;
    }
    public bool NeedsRelease => owned || escapeOwned;
    public TranscribeRoute Mouse(int message)
    {
        if (!active) return default;
        if (trigger == TranscribeTrigger.MiddleMouse)
            return message is 0x207 or 0x208 ? new(true, message) : message == 0x200 ? new(false, message) : default;
        return message == 0x200 && owned && !canceled ? new(false, message) : default;
    }
    public TranscribeRoute Key(int key, bool down, bool modified = false)
    {
        if (key == 27)
        {
            if (escapeOwned) { if (!down) escapeOwned = false; return new(true); }
            if (active && down && owned && !canceled) { escapeOwned = true; Cancel(); return new(true, -1); }
            return default;
        }
        if (key != 44 || trigger != TranscribeTrigger.PrintScreen) return default;
        if (down)
        {
            if (held) return new(owned);
            held = true;
            if (!active || modified) return default;
            owned = true; canceled = false; return new(true, 0x207);
        }
        held = false;
        if (!owned) return default;
        owned = false;
        return new(true, active && !canceled ? 0x208 : 0);
    }
    public void Cancel() => canceled = true;
    public void Stop() { active = false; Cancel(); }
}
