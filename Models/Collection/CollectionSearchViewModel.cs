namespace DiscogScrobblerMVC.Models;

public class CollectionSearchViewModel
{
    public string Query { get; set; } = "";

    public IReadOnlyList<CollectionSearchArtistResult> Artists { get; set; } = [];

    public IReadOnlyList<CollectionSearchReleaseResult> Releases { get; set; } = [];
}

public class CollectionSearchArtistResult
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CollectionSearchReleaseResult
{
    public int ReleaseId { get; set; }
    public string Album { get; set; } = "";
    public int? Year { get; set; }
    public string ArtistDisplay { get; set; } = "";
    public string? CoverUrl { get; set; }
}
