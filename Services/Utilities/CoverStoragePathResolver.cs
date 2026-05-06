namespace DiscogScrobblerMVC.Services;

public static class CoverStoragePathResolver
{
    public const string DefaultRelativeFolder = "images";

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
