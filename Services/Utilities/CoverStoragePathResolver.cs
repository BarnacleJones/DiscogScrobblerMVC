using System.Text;

namespace DiscogScrobblerMVC.Services.Utilities;

public static class CoverStoragePathResolver
{
    public const string DefaultRelativeFolder = "images";

    public const string SharedArtistProfileSubfolder = "artists";

    public const string SharedLabelProfileSubfolder = "labels";

    public static bool TryGetDiscogsCoverSubfolderName(string? discogsUsernameFromProfile, out string coverSubfolderName)
    {
        coverSubfolderName = "";
        var trimmed = discogsUsernameFromProfile?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch is '/' or '\\')
            {
                builder.Append('_');
                continue;
            }

            if (Array.IndexOf(invalid, ch) >= 0)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(ch);
        }

        var candidate = builder.ToString().Trim();
        if (candidate.Length == 0 || candidate is "." or "..")
            return false;

        // Unlikely but if the discogs username was either of these reserved names we don't want to use it.
        if (string.Equals(candidate, SharedArtistProfileSubfolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, SharedLabelProfileSubfolder, StringComparison.OrdinalIgnoreCase))
            return false;

        coverSubfolderName = candidate;
        return true;
    }

    public static string ResolveImageBasePath(string contentRoot, string? configured)
    {
        var trimmed = configured?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            trimmed = DefaultRelativeFolder;

        var resolved = Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(contentRoot, trimmed));

        var root = NormalizePath(contentRoot);

        // Avoid writing into the application/content root itself.
        if (string.Equals(NormalizePath(resolved), root, StringComparison.Ordinal))
            resolved = Path.GetFullPath(Path.Combine(resolved, DefaultRelativeFolder));

        return resolved;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
