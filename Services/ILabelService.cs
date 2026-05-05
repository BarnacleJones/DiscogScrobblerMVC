using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface ILabelService
{
    Task<LabelViewModel?> GetLabel(int id);
}
