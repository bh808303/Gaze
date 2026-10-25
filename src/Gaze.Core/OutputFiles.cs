using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Gaze.Core;

public static class OutputFiles
{
    public static bool Includes(OutputCategory category, string path) =>
        (category == OutputCategory.Screenshots ? new[] { ".png", ".zip" } : new[] { ".mp4" })
        .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static void Clean(string root, OutputCategory category, Func<string, bool> confirm, Action<string> recycle)
    {
        var directory = OutputStore.Folder(root, category);
        // Do not traverse a redirected category directory or recycle redirected files.
        if (!Directory.Exists(directory)) return;
        using var identity = PinDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Output category is a linked directory; cleanup refused.");
        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(p => Includes(category, p) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0).ToArray();
        if (files.Length == 0 || !confirm($"Move {files.Length} {category} files to the Recycle Bin?\n\n{directory}\n\nOnly matching top-level files will be recycled.")) return;
        foreach (var file in files)
        {
            identity.Verify();
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) recycle(file);
        }
    }

    // Pin every path component before enumerating and keep it pinned through the
    // modal confirmation and the last recycle callback. A pathname recheck alone
    // leaves another swap window. OPEN_REPARSE_POINT inspects the object itself.
    private static DirectoryHandles PinDirectory(string directory)
    {
        if (!OperatingSystem.IsWindows())
            throw new IOException("Safe output cleanup requires Windows directory handles.");
        var handles = new DirectoryHandles();
        try
        {
            var components = new Stack<string>();
            for (var current = new DirectoryInfo(directory); current != null; current = current.Parent)
                components.Push(current.FullName);
            foreach (var path in components)
            {
                var handle = CreateFileW(path, 0x80, 0x1 | 0x2, IntPtr.Zero, 3,
                    0x02000000 | 0x00200000, IntPtr.Zero);
                handles.Items.Add(handle);
                handles.Paths.Add(path);
                if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 9, out AttributeTagInfo info, 8)
                    || (info.Attributes & (uint)FileAttributes.Directory) == 0
                    || (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Output directory identity could not be secured; cleanup refused.");
            }
            handles.Verify();
            return handles;
        }
        catch { handles.Dispose(); throw; }
    }

    private sealed class DirectoryHandles : IDisposable
    {
        internal readonly List<SafeFileHandle> Items = [];
        internal readonly List<string> Paths = [];
        internal void Verify()
        {
            for (var i = 0; i < Items.Count; i++)
            {
                using var current = CreateFileW(Paths[i], 0x80, 0x1 | 0x2, IntPtr.Zero, 3,
                    0x02000000 | 0x00200000, IntPtr.Zero);
                if (current.IsInvalid
                    || !GetFileInformationByHandleEx(current, 9, out AttributeTagInfo attributes, 8)
                    || (attributes.Attributes & (uint)FileAttributes.ReparsePoint) != 0
                    || !GetFileInformationByHandleEx(Items[i], 18, out FileIdInfo original, 24)
                    || !GetFileInformationByHandleEx(current, 18, out FileIdInfo now, 24)
                    || !original.Equals(now))
                    throw new IOException("Output directory changed; cleanup refused.");
            }
        }
        public void Dispose() { for (var i = Items.Count - 1; i >= 0; i--) Items[i].Dispose(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTagInfo { public uint Attributes; public uint ReparseTag; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo { public ulong Volume; public ulong Low; public ulong High; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,
        int informationClass, out AttributeTagInfo information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,
        int informationClass, out FileIdInfo information, uint size);
}
