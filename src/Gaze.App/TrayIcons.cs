using Gaze.Core;

namespace Gaze.App;

internal sealed class TrayIcons : IDisposable
{
    private readonly Icon passive = Load("gaze-silent");
    private readonly Icon armed = Load("gaze-armed");
    private readonly Icon recording = Load("gaze-recording");

    private static Icon Load(string name)
    {
        using var stream = typeof(TrayIcons).Assembly.GetManifestResourceStream($"Gaze.Icons.{name}.ico")
            ?? throw new InvalidOperationException($"Missing tray icon: {name}");
        using var source = new Icon(stream, SystemInformation.SmallIconSize);
        return (Icon)source.Clone();
    }

    internal Icon For(SessionState state) => state switch
    {
        SessionState.Recording or SessionState.Paused => recording,
        SessionState.Armed or SessionState.Saving => armed,
        _ => passive
    };

    public void Dispose() { passive.Dispose(); armed.Dispose(); recording.Dispose(); }
}
