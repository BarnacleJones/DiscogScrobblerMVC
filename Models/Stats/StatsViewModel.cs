namespace DiscogScrobblerMVC.Models;

public class StatsViewModel
{
    /// <summary>True when no releases at all are owned (drives an empty-state hint in the view).</summary>
    public bool HasAnyReleases { get; set; }

    /// <summary>Releases owned by the user (denominator for avg tracks per release).</summary>
    public int OwnedReleaseCount { get; set; }

    /// <summary>Releases that have at least one track row ingested.</summary>
    public int IngestedReleaseCount { get; set; }

    /// <summary>OwnedReleaseCount minus IngestedReleaseCount; used for the "syncing N releases…" hint.</summary>
    public int PendingReleaseCount => Math.Max(0, OwnedReleaseCount - IngestedReleaseCount);

    public int TotalTracks { get; set; }

    /// <summary>Average track length in seconds across tracks with a known DurationSeconds.</summary>
    public double? AverageTrackSeconds { get; set; }

    /// <summary>Number of tracks contributing to <see cref="AverageTrackSeconds"/>.</summary>
    public int TrackDurationSampleSize { get; set; }

    public double? AverageTracksPerRelease { get; set; }

    public IReadOnlyList<LetterBucketViewModel> LetterDistribution { get; set; } = [];
    public IReadOnlyList<NameCountViewModel> TopStyles { get; set; } = [];
    public IReadOnlyList<NameCountViewModel> TopGenres { get; set; } = [];
    public IReadOnlyList<TopArtistViewModel> TopArtists { get; set; } = [];

    /// <summary>Releases credited to the "Various" placeholder, omitted from <see cref="TopArtists"/>.</summary>
    public int VariousReleaseCount { get; set; }
}

public record LetterBucketViewModel(char Letter, int Count);

public record NameCountViewModel(string Name, int Count);

public record TopArtistViewModel(int Id, string Name, int ReleaseCount);
