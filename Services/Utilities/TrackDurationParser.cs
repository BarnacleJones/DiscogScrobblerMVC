using System.Globalization;

namespace DiscogScrobblerMVC.Services;

/// <summary>
/// Parses Discogs-style track duration strings ("MM:SS" or "HH:MM:SS") into seconds.
/// Used both at write time (to populate <c>Track.DurationSeconds</c>) and by the scrobble
/// pipeline so the two paths can't drift.
/// </summary>
public static class TrackDurationParser
{
    public static int? TryParseSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return null;

        var parts = duration.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ss))
            return mm * 60 + ss;

        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hh) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm2) &&
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ss2))
            return hh * 3600 + mm2 * 60 + ss2;

        return null;
    }
}
