using Gaze.Core;

internal static class StartupChecks
{
    internal static void Run(Action<string, Action> test)
    {
        test("Startup enables a quoted portable path, repairs it, and disables only Gaze", () =>
        {
            var entries = new Dictionary<string, string> { ["OtherApp"] = "untouched" };
            var startup = new SignInStartup(() => entries.GetValueOrDefault("Gaze"), command =>
            { if (command == null) entries.Remove("Gaze"); else entries["Gaze"] = command; }, @"C:\Portable Apps\Gaze.App.exe");
            Check(!startup.IsEnabled, "Fresh startup must be off");
            startup.SetEnabled(true);
            Check(entries["Gaze"] == "\"C:\\Portable Apps\\Gaze.App.exe\"" && startup.IsEnabled, "Executable must be absolute and quoted");
            entries["Gaze"] = "\"C:\\Old\\Gaze.App.exe\"";
            Check(startup.IsEnabled, "Existing registration must be shown");
            startup.SetEnabled(true);
            Check(entries["Gaze"] == "\"C:\\Portable Apps\\Gaze.App.exe\"", "Re-enabling must repair path");
            startup.SetEnabled(false); startup.SetEnabled(false);
            Check(!startup.IsEnabled && entries["OtherApp"] == "untouched", "Disable must preserve unrelated entries");
            entries["Gaze"] = "external change";
            Check(startup.IsEnabled, "State must be reread rather than cached");
        });
        test("Startup failures propagate and never claim an enabled registration", () =>
        {
            var startup = new SignInStartup(() => null, _ => throw new UnauthorizedAccessException("denied"), @"C:\Gaze\Gaze.App.exe");
            Throws<UnauthorizedAccessException>(() => startup.SetEnabled(true));
            Check(!startup.IsEnabled, "Failed enable must remain off");
            var ignored = new SignInStartup(() => null, _ => { }, @"C:\Gaze\Gaze.App.exe");
            Throws<InvalidOperationException>(() => ignored.SetEnabled(true));
            var existing = new SignInStartup(() => "existing", _ => throw new IOException("denied"), @"C:\Gaze\Gaze.App.exe");
            Throws<IOException>(() => existing.SetEnabled(false));
            Check(existing.IsEnabled, "Failed disable must remain enabled");
            var unreadable = new SignInStartup(() => throw new UnauthorizedAccessException("read denied"), _ => { }, null);
            Throws<UnauthorizedAccessException>(() => _ = unreadable.IsEnabled);
        });
        test("Startup rejects development hosts and invalid paths without writing", () =>
        {
            foreach (var path in new string?[] { null, "", "Gaze.App.exe", @"C:\SDK\dotnet.exe", @"C:\Gaze\Gaze.dll", "C:\\bad\"path.exe" })
            {
                bool wrote = false;
                var startup = new SignInStartup(() => null, _ => wrote = true, path);
                Throws<InvalidOperationException>(() => startup.SetEnabled(true));
                Check(!wrote, "Invalid launch must not register");
            }
        });
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
