using System.Globalization;

namespace Gaze.Core;

public static class RecordingName
{
    public static string Create(DateTime start, DateTime end, TimeSpan duration)
    {
        if (end < start || duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        return $"Gaze_d{start:ddMMyy}_t{start:HHmm}-{end:HHmm}_{Math.Floor(duration.TotalMinutes).ToString("0", CultureInfo.InvariantCulture)}min.mp4";
    }
}
