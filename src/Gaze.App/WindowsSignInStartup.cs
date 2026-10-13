using Gaze.Core;
using Microsoft.Win32;

namespace Gaze.App;

internal static class WindowsSignInStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "Gaze";
    internal static SignInStartup Create() => new(Read, Write, Environment.ProcessPath);
    private static string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var value = key?.GetValue(EntryName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value == null ? null : value as string ?? "";
    }
    private static void Write(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Cannot open the current-user Windows startup key.");
        if (command == null) key.DeleteValue(EntryName, throwOnMissingValue: false);
        else key.SetValue(EntryName, command, RegistryValueKind.String);
    }
}
