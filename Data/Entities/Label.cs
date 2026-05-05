namespace DiscogScrobblerMVC.Data.Entities;

public class Label
{
    public int Id { get; set; }
    public int? DiscogsLabelId { get; set; }
    public string Name { get; set; } = "";
    public ICollection<Release> Releases { get; set; } = [];
}
