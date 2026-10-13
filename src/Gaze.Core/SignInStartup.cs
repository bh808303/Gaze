namespace Gaze.Core;

/// <summary>Startup policy with an injectable, Gaze-only registration boundary.</summary>
public sealed class SignInStartup(Func<string?> read, Action<string?> write, string? executablePath)
{
    public bool IsEnabled => read() != null;

    public void SetEnabled(bool enabled)
    {
        string? command = null;
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath)
                || !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(executablePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase)
                || executablePath.Contains('"'))
                throw new InvalidOperationException("Start at Windows sign-in requires the Gaze app executable. Launch Gaze.App.exe instead of dotnet, then enable startup.");
            command = $"\"{executablePath}\"";
        }
        if (read() != command) write(command);
        if (read() != command) throw new InvalidOperationException("Windows did not retain the requested Gaze startup registration.");
    }
}
