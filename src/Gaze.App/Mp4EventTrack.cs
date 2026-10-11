using System.Buffers.Binary;
using System.Text;

namespace Gaze.App;

internal static class Mp4EventTrack
{
    private sealed record Box(long Start, long Size, int Header, string Type)
    {
        internal long Payload => Start + Header;
        internal long End => Start + Size;
    }
    private static List<Box> Boxes(FileStream file, long start, long end)
    {
        var boxes = new List<Box>(); var header = new byte[16];
        while (start < end)
        {
            if (end - start < 8) throw new InvalidDataException("Truncated MP4 box header.");
            file.Position = start; file.ReadExactly(header.AsSpan(0, 8));
            long size = BinaryPrimitives.ReadUInt32BigEndian(header); int length = 8;
            string type = Encoding.ASCII.GetString(header, 4, 4);
            if (size == 1)
            {
                if (end - start < 16) throw new InvalidDataException("Truncated extended MP4 box.");
                file.ReadExactly(header.AsSpan(8, 8)); size = checked((long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8))); length = 16;
            }
            else if (size == 0) size = end - start;
            if (size < length || size > end - start) throw new InvalidDataException("Invalid MP4 box size.");
            boxes.Add(new(start, size, length, type)); start += size;
        }
        return boxes;
    }
    // FFmpeg 7.1's MOV muxer enables the first subtitle track even with
    // disposition=0. Clear only that track's tkhd enabled bit after remuxing.
    internal static void Disable(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var movie = Boxes(file, 0, file.Length).Single(b => b.Type == "moov");
        foreach (var track in Boxes(file, movie.Payload, movie.End).Where(b => b.Type == "trak"))
        {
            var children = Boxes(file, track.Payload, track.End); var media = children.Single(b => b.Type == "mdia");
            var handler = Boxes(file, media.Payload, media.End).Single(b => b.Type == "hdlr");
            if (handler.Size - handler.Header is < 24 or > 4096) continue;
            var bytes = new byte[checked((int)(handler.Size - handler.Header))]; file.Position = handler.Payload; file.ReadExactly(bytes);
            if (Encoding.UTF8.GetString(bytes, 24, bytes.Length - 24).TrimEnd('\0') != "Gaze events") continue;
            var header = children.Single(b => b.Type == "tkhd");
            if (header.Size - header.Header < 4) throw new InvalidDataException("Truncated MP4 track flags.");
            file.Position = header.Payload + 3; int flags = file.ReadByte();
            file.Position--; file.WriteByte((byte)(flags & ~1)); return;
        }
        throw new InvalidDataException("Gaze event track is missing from the rewritten MP4.");
    }
}
