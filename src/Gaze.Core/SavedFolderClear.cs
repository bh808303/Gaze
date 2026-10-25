namespace Gaze.Core;

public static class SavedFolderClear
{
    public static void Run(string folder, Func<string, bool> confirm, Action<string> recycle)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        var directory = Path.GetFullPath(folder);
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(file => new[] { ".mp4", ".png", ".zip" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToArray()
            : [];
        if (files.Length == 0) return;
        if (!confirm($"Move all {files.Length} top-level MP4, PNG and ZIP files in this folder to the Recycle Bin?\n\n{directory}")) return;
        foreach (var file in files)
        {
            var fullPath = Path.GetFullPath(file);
            if (!string.Equals(Path.GetDirectoryName(fullPath), Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Recording is outside the save folder.");
            if (File.Exists(fullPath)) recycle(fullPath);
        }
    }
}
