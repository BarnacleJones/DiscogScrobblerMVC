using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers
{
    public class CollectionController : Controller
    {
        // GET /Collection
        public IActionResult Index()
        {
            return View();
        }

        // GET /Collection/GetCollection
        // Called by DataTables via AJAX
        [HttpGet]
        public IActionResult GetCollection()
        {
            // TODO: replace with real Discogs service call
            var items = new List<CollectionItemDto>
            {
                new() { ReleaseId = 1, Artist = "Talking Heads",     Album = "Remain in Light",   Year = 1980, DateAdded = new DateTime(2023, 4, 12) },
                new() { ReleaseId = 2, Artist = "My Bloody Valentine",Album = "Loveless",           Year = 1991, DateAdded = new DateTime(2023, 6, 3)  },
                new() { ReleaseId = 3, Artist = "Radiohead",          Album = "OK Computer",        Year = 1997, DateAdded = new DateTime(2024, 1, 19) },
                new() { ReleaseId = 4, Artist = "Massive Attack",     Album = "Blue Lines",         Year = 1991, DateAdded = new DateTime(2024, 2, 7)  },
                new() { ReleaseId = 5, Artist = "Portishead",         Album = "Dummy",              Year = 1994, DateAdded = new DateTime(2024, 3, 22) },
            };

            return Json(items);
        }

        // POST /Collection/Scrobble
        [HttpPost]
        public IActionResult Scrobble(int releaseId)
        {
            // TODO: call Last.fm scrobble service
            // For now just return success
            return Ok();
        }
    }

    public class CollectionItemDto
    {
        public int ReleaseId { get; set; }
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public int Year { get; set; }
        public DateTime DateAdded { get; set; }
    }
}