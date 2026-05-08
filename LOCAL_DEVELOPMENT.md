# Local development

Prereqs: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js](https://nodejs.org/) (esbuild runs on `dotnet build`), a Discogs app (**consumer key** and **consumer secret** in config; each user adds their own **Generate token** in **Settings**), and Last.fm API key/secret if you need scrobbling before connecting in the UI.

From the repo root (the directory containing `DiscogScrobblerMVC.csproj`):

```bash
npm ci   # or npm install
```

Create **`appsettings.Local.json`** here (git-ignored — use at least `{}` as valid JSON). Example:

```json
{
  "Discogs": {
    "ConsumerKey": "…",
    "ConsumerSecret": "…"
  },
  "LastFm": {
    "ApiKey": "…",
    "ApiSecret": "…"
  }
}
```

Each user’s **Last.fm OAuth session** (session key and display name) is stored in SQLite after **Settings → Connect Last.fm**, not in config.

Cover art caches under **`App:ImageBasePath`** (defaults to **`images/`** beside the project; see **`appsettings.json`**). Inside that folder: per-account release covers in **`images/{sanitized-discogs-username}/`**, shared **`images/artists/`** for artist portraits, **`images/labels/`** for label logos. Missing **Discogs username** in Settings → no local file path for releases (URLs use Discogs). Reserved names **`artists`** / **`labels`** are not allowed as sanitized usernames. Layout is described fully in **[README.md](README.md)**.

Override **`ConnectionStrings:DefaultConnection`** if you do not want the default SQLite file beside the project.

```bash
dotnet run
```

Register the first account (that user is **Admin**). To test pending users, register a second account: it stays **pending** until you approve it under **Settings**. Then open **Settings**, set **Discogs username** and **personal access token**, connect Last.fm if needed, and use sync controls (a daily background sync also runs for each user who has both username and token saved).

Useful commands from the same project directory:

```bash
dotnet build    # also runs npm run build
npm run build   # rebuild generated wwwroot/js files only
npm run watch   # watch Scripts/*.ts and rebuild frontend bundles
```

Frontend source lives in **`Scripts/*.ts`**. Do not edit **`wwwroot/js/*.js`** directly; those files are generated.

**Security:** Identity is aimed at trusted LAN use, not hardened for public internet; relax password rules vs production defaults live in **`Program.cs`** — tighten before wider exposure. Discogs personal access tokens and Last.fm OAuth session keys are stored **in clear** in SQLite by design for hobby use; treat backups of `app.db` like secrets. See **[README.md](README.md)**.

**Docker / LAN hosting:** **[HOSTING.md](HOSTING.md)** creates a compose setup in a parent runtime folder, next to the cloned **`DiscogScrobblerMVC/`** repo directory.
