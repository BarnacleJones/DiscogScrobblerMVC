namespace DiscogScrobblerMVC.Data.Entities;

public class ReleaseStyle
{
    public int ReleaseId { get; set; }
    public Release Release { get; set; } = null!;
    public int StyleId { get; set; }
    public Style Style { get; set; } = null!;
}
