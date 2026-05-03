using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;

namespace DiscogScrobblerMVC.Services;

// Services/DiscogsService.cs
using System.Net.Http.Headers;
using System.Text.Json;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;

public interface IDiscogsService
{
    Task SyncCollectionAsync(string discogsUsername, string userId);
}

public class DiscogsService : IDiscogsService
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DiscogsService> _logger;

    public DiscogsService(HttpClient http, ApplicationDbContext db, ILogger<DiscogsService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task SyncCollectionAsync(string discogsUsername, string userId)
    {
        var page = 1;
        const int perPage = 100;
        var hasMore = true;

        while (hasMore)
        {
            var url = $"users/{discogsUsername}/collection/folders/0/releases" +
                      $"?page={page}&per_page={perPage}&sort=added&sort_order=desc";

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var releases = root.GetProperty("releases");
            var pagination = root.GetProperty("pagination");

            foreach (var item in releases.EnumerateArray())
            {
                var basicInfo = item.GetProperty("basic_information");
                var discogsId = item.GetProperty("id").GetInt32();

                // Grab primary image if present
                string? coverUrl = null;
                if (basicInfo.TryGetProperty("cover_image", out var coverEl))
                    coverUrl = coverEl.GetString();

                // Labels → take the first
                string? label = null;
                if (basicInfo.TryGetProperty("labels", out var labels) &&
                    labels.GetArrayLength() > 0)
                    label = labels[0].GetProperty("name").GetString();

                // Formats → take the first
                string? format = null;
                if (basicInfo.TryGetProperty("formats", out var formats) &&
                    formats.GetArrayLength() > 0)
                    format = formats[0].GetProperty("name").GetString();

                // Artist — join multiple
                var artistName = "Unknown";
                if (basicInfo.TryGetProperty("artists", out var artists) &&
                    artists.GetArrayLength() > 0)
                    artistName = string.Join(", ",
                        artists.EnumerateArray()
                               .Select(a => a.GetProperty("name").GetString()?.Trim()));

                var dateAdded = item.TryGetProperty("date_added", out var da)
                    ? DateTime.Parse(da.GetString()!)
                    : DateTime.UtcNow;

                // Upsert
                var existing = await _db.Releases
                    .FirstOrDefaultAsync(r => r.DiscogsReleaseId == discogsId
                                           && r.UserId == userId);
                if (existing is null)
                {
                    _db.Releases.Add(new Release
                    {
                        DiscogsReleaseId = discogsId,
                        Artist = artistName,
                        Album = basicInfo.GetProperty("title").GetString() ?? "",
                        Year = basicInfo.TryGetProperty("year", out var yr) ? yr.GetInt32() : 0,
                        CoverUrl = coverUrl,
                        Format = format,
                        RecordLabel = label,
                        DateAdded = dateAdded,
                        UserId = userId,
                    });
                }
                else
                {
                    // Update cover URL in case it changed (Discogs CDN URLs rotate)
                    existing.CoverUrl = coverUrl;
                }
            }

            await _db.SaveChangesAsync();

            var pages = pagination.GetProperty("pages").GetInt32();
            hasMore = page < pages;
            page++;
        }

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }
}