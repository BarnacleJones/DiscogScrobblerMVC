using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface ICollectionBrowseService
{
    Task<CollectionBrowseViewModel> GetByYearAsync(string userId, int year, CancellationToken cancellationToken = default);

    Task<CollectionBrowseViewModel?> GetByGenreIdAsync(string userId, int genreId, CancellationToken cancellationToken = default);

    Task<CollectionBrowseViewModel?> GetByStyleIdAsync(string userId, int styleId, CancellationToken cancellationToken = default);
}
