using Microsoft.AspNetCore.Mvc;
using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Controllers;

public class ReleaseController : Controller
{
    // TODO: replace with real Discogs service call
    private static readonly Dictionary<int, ReleaseViewModel> _releases = new()
    {
        [1] = new ReleaseViewModel
        {
            ReleaseId = 1,
            Artist = "Talking Heads",
            Album = "Remain in Light",
            Year = 1980,
            CoverUrl = "https://placehold.co/400x400/1e1e21/5a5955?text=Remain+in+Light",
            Have = 8234,
            Want = 3102,
            RecordCompanies = "Sire Records",
            Format = "LP, Album",
            Genres = ["Rock", "Funk / Soul"],
            Styles = ["New Wave", "Art Rock", "Funk", "Afrobeat"],
            Tracklist =
            [
                new() { Position = "A1", Title = "Born Under Punches (The Heat Goes On)", Duration = "5:47" },
                new() { Position = "A2", Title = "Crosseyed and Painless",                Duration = "4:44" },
                new() { Position = "A3", Title = "The Great Curve",                       Duration = "6:24" },
                new() { Position = "B1", Title = "Once in a Lifetime",                   Duration = "4:20" },
                new() { Position = "B2", Title = "Houses in Motion",                     Duration = "4:29" },
                new() { Position = "B3", Title = "Seen and Not Seen",                    Duration = "3:21" },
                new() { Position = "B4", Title = "Listening Wind",                       Duration = "4:08" },
                new() { Position = "B5", Title = "The Overload",                         Duration = "5:52" },
            ]
        },
        [2] = new ReleaseViewModel
        {
            ReleaseId = 2,
            Artist = "My Bloody Valentine",
            Album = "Loveless",
            Year = 1991,
            CoverUrl = "https://placehold.co/400x400/1e1e21/5a5955?text=Loveless",
            Have = 12501,
            Want = 7843,
            RecordCompanies = "Creation Records",
            Format = "LP, Album",
            Genres = ["Rock"],
            Styles = ["Shoegaze", "Indie Rock", "Dream Pop"],
            Tracklist =
            [
                new() { Position = "A1", Title = "Only Shallow",       Duration = "4:17" },
                new() { Position = "A2", Title = "Loomer",             Duration = "2:38" },
                new() { Position = "A3", Title = "Touched",            Duration = "1:58" },
                new() { Position = "A4", Title = "To Here Knows When", Duration = "5:31" },
                new() { Position = "A5", Title = "When You Sleep",     Duration = "4:11" },
                new() { Position = "B1", Title = "I Only Said",        Duration = "5:34" },
                new() { Position = "B2", Title = "Come in Alone",      Duration = "3:58" },
                new() { Position = "B3", Title = "Sometimes",          Duration = "5:20" },
                new() { Position = "B4", Title = "Blown",              Duration = "3:36" },
                new() { Position = "B5", Title = "Dreaming",           Duration = "3:13" },
            ]
        },
        [3] = new ReleaseViewModel
        {
            ReleaseId = 3,
            Artist = "Radiohead",
            Album = "OK Computer",
            Year = 1997,
            CoverUrl = "https://placehold.co/400x400/1e1e21/5a5955?text=OK+Computer",
            Have = 31205,
            Want = 14920,
            RecordCompanies = "Parlophone, Capitol Records",
            Format = "LP, Album",
            Genres = ["Rock", "Electronic"],
            Styles = ["Alternative Rock", "Art Rock", "Post-Brit Pop"],
            Tracklist =
            [
                new() { Position = "A1", Title = "Airbag",                          Duration = "4:44" },
                new() { Position = "A2", Title = "Paranoid Android",                Duration = "6:23" },
                new() { Position = "A3", Title = "Subterranean Homesick Alien",     Duration = "4:27" },
                new() { Position = "A4", Title = "Exit Music (For a Film)",         Duration = "4:24" },
                new() { Position = "A5", Title = "Let Down",                        Duration = "4:59" },
                new() { Position = "A6", Title = "Karma Police",                    Duration = "4:21" },
                new() { Position = "B1", Title = "Fitter Happier",                  Duration = "1:57" },
                new() { Position = "B2", Title = "Electioneering",                  Duration = "3:50" },
                new() { Position = "B3", Title = "Climbing Up the Walls",           Duration = "4:45" },
                new() { Position = "B4", Title = "No Surprises",                    Duration = "3:48" },
                new() { Position = "B5", Title = "Lucky",                           Duration = "4:19" },
                new() { Position = "B6", Title = "The Tourist",                     Duration = "5:24" },
            ]
        },
        [4] = new ReleaseViewModel
        {
            ReleaseId = 4,
            Artist = "Massive Attack",
            Album = "Blue Lines",
            Year = 1991,
            CoverUrl = "https://placehold.co/400x400/1e1e21/5a5955?text=Blue+Lines",
            Have = 9821,
            Want = 5634,
            RecordCompanies = "Wild Bunch Records, Circa Records",
            Format = "LP, Album",
            Genres = ["Electronic", "Hip Hop"],
            Styles = ["Trip Hop", "Soul", "Downtempo"],
            Tracklist =
            [
                new() { Position = "A1", Title = "Safe from Harm",                        Duration = "5:18" },
                new() { Position = "A2", Title = "One Love",                              Duration = "4:44" },
                new() { Position = "A3", Title = "Blue Lines",                            Duration = "4:11" },
                new() { Position = "A4", Title = "Be Thankful for What You've Got",       Duration = "3:28" },
                new() { Position = "B1", Title = "Five Man Army",                         Duration = "6:06" },
                new() { Position = "B2", Title = "Unfinished Sympathy",                   Duration = "5:07" },
                new() { Position = "B3", Title = "Daydreaming",                           Duration = "4:43" },
                new() { Position = "B4", Title = "Lately",                                Duration = "5:36" },
                new() { Position = "B5", Title = "Hymn of the Big Wheel",                 Duration = "6:03" },
            ]
        },
    };

    [Route("release/{id}")]
    public IActionResult Index(int id)
    {
        if (!_releases.TryGetValue(id, out var release))
            return NotFound();

        return View(release);
    }

    [HttpPost]
    public IActionResult Scrobble(int releaseId)
    {
        // TODO: call Last.fm scrobble service
        return Ok();
    }
}
