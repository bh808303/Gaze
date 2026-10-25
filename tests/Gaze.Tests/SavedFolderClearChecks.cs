using Gaze.Core;

static class SavedFolderClearChecks
{
    public static void Run(Action<string, Action> test)
    {
        test("Saved-folder Clear confirms exact top-level MP4/PNG/ZIP scope before fake recycling", () =>
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "clear-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "nested"));
            try
            {
                string[] eligible = ["video.mp4", "upper.MP4", "image.png", "mixed.PnG", "batch.zip", "archive.ZiP"];
                string[] excluded = ["notes.txt", "image.png.bak", "movie.mp4x", "zip", "working.tmp", "nested/hidden.png", "nested/hidden.mp4", "nested/hidden.zip"];
                foreach (var name in eligible.Concat(excluded)) File.WriteAllText(Path.Combine(folder, name), "fixture");
                var recycled = new List<string>();
                var confirmations = 0;
                SavedFolderClear.Run(folder, message =>
                {
                    confirmations++;
                    if (recycled.Count != 0) throw new Exception("Recycled before confirmation");
                    foreach (var expected in new[] { "6", "MP4", "PNG", "ZIP", folder })
                        if (!message.Contains(expected, StringComparison.Ordinal)) throw new Exception($"Confirmation missing {expected}: {message}");
                    return true;
                }, recycled.Add);
                if (confirmations != 1 || !recycled.ToHashSet(StringComparer.Ordinal).SetEquals(eligible.Select(name => Path.Combine(folder, name))))
                    throw new Exception("Confirmed action must receive only all six eligible top-level paths");
                recycled.Clear();
                SavedFolderClear.Run(folder, _ => false, recycled.Add);
                if (recycled.Count != 0) throw new Exception("Cancellation recycled files");
                if (Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length != eligible.Length + excluded.Length)
                    throw new Exception("Fake recycling changed fixture files");
            }
            finally { Directory.Delete(folder, true); }
        });
        test("Saved-folder Clear empty, missing and blank folders invoke neither callback", () =>
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "clear-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var path in new[] { folder, Path.Combine(folder, "missing"), "" })
                    SavedFolderClear.Run(path, _ => throw new Exception("Unexpected confirmation"), _ => throw new Exception("Unexpected recycling"));
                File.WriteAllText(Path.Combine(folder, "keep.txt"), "keep");
                SavedFolderClear.Run(folder, _ => throw new Exception("Unexpected confirmation for ineligible files"), _ => throw new Exception("Unexpected recycling"));
            }
            finally { Directory.Delete(folder, true); }
        });
    }
}
