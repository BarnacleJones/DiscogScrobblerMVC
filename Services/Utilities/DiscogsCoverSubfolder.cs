using DiscogScrobblerMVC.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services.Utilities;

public static class DiscogsCoverSubfolder
{
    public static async Task<string?> TryGetNameForSignedInUserAsync(
        ApplicationDbContext db,
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        var discogsUsernameStoredOnUser = await db.Users.AsNoTracking()
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.DiscogsUsername)
            .FirstOrDefaultAsync(cancellationToken);

        return TryGetNameFromDiscogsUsername(discogsUsernameStoredOnUser);
    }

    public static string? TryGetNameFromDiscogsUsername(string? discogsUsernameFromProfile) =>
        CoverStoragePathResolver.TryGetDiscogsCoverSubfolderName(discogsUsernameFromProfile, out var coverSubfolderName)
            ? coverSubfolderName
            : null;
}
