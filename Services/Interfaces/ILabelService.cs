using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface ILabelService
{
    Task<LabelViewModel?> GetLabel(int id, string userId, CancellationToken cancellationToken = default);
}
