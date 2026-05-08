using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionItemViewModel>> GetCollectionItemsAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HomeRecentReleaseViewModel>> GetRecentCollectionReleasesAsync(
        string userId, int take, CancellationToken cancellationToken = default);

    Task<CollectionSearchViewModel> SearchCollectionAsync(
        string userId, string? query, CancellationToken cancellationToken = default);
}
