namespace DiscogScrobblerMVC.Data.Entities;

public class Style
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";

    public ICollection<ReleaseStyle> ReleaseLinks { get; set; } = [];
}
