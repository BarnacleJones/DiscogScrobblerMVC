namespace DiscogScrobblerMVC.Data.Entities;

public class Release
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public int Year { get; set; }
    public string? CoverUrl { get; set; }
    public byte[]? CoverImage { get; set; } //todo fetch the image in the background task
    public string? Format { get; set; }
    public string? RecordLabel { get; set; }
    public DateTime DateAdded { get; set; }
    public string UserId { get; set; } = "";
}