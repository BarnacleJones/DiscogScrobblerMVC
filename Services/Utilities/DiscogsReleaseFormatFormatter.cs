using DiscogsApiClient.Contract.Release;

namespace DiscogScrobblerMVC.Services.Utilities;

public static class DiscogsReleaseFormatFormatter
{
    /// <summary>
    /// Builds a single display string from Discogs <see cref="ReleaseFormat"/> entries (name, qty, descriptions).
    /// </summary>
    public static string? BuildFormatSummary(IReadOnlyList<ReleaseFormat>? discogsFormats)
    {
        if (discogsFormats is not { Count: > 0 })
            return null;

        var displayFragmentPerFormat = new List<string>(discogsFormats.Count);
        foreach (var formatEntry in discogsFormats)
        {
            var lineForThisFormat = formatEntry.Name?.Trim();
            if (string.IsNullOrEmpty(lineForThisFormat))
                continue;

            var quantityText = formatEntry.Count?.Trim();
            if (!string.IsNullOrEmpty(quantityText) && quantityText != "1")
                lineForThisFormat += $" ×{quantityText}";

            if (formatEntry.Descriptions is { Count: > 0 })
            {
                var descriptionLabelsJoined = string.Join(
                    ", ",
                    formatEntry.Descriptions
                        .Where(description => !string.IsNullOrWhiteSpace(description))
                        .Select(description => description.Trim()));
                if (!string.IsNullOrEmpty(descriptionLabelsJoined))
                    lineForThisFormat += $" — {descriptionLabelsJoined}";
            }

            displayFragmentPerFormat.Add(lineForThisFormat);
        }

        return displayFragmentPerFormat.Count == 0
            ? null
            : string.Join("; ", displayFragmentPerFormat);
    }
}
