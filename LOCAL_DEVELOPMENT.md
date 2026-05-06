# Local development

Prereqs: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js](https://nodejs.org/) (esbuild runs on `dotnet build`), a Discogs app (consumer key/secret + **Generate token**), and Last.fm API key/secret if you need scrobbling before connecting in the UI.

From the repo root (the directory containing `DiscogScrobblerMVC.csproj`):

```bash
npm ci   # or npm install
```

Create **`appsettings.Local.json`** here (git-ignored — use at least `{}` as valid JSON). Example:

```json
{
  "Discogs": {
    "ConsumerKey": "…",
    "ConsumerSecret": "…",
    "PersonalAccessToken": "…"
  },
  "LastFm": {
    "ApiKey": "…",
    "ApiSecret": "…"
  }
}
```

`PersonalAccessToken` and `UserToken` are equivalent (Discogs developer token). Prefer **Settings → Connect Last.fm** in the app over storing `SessionKey` / legacy password auth in config.

Override **`ConnectionStrings:DefaultConnection`** if you do not want the default SQLite file beside the project.

```bash
dotnet run
```

Register, open **Settings**, set **Discogs username**, connect Last.fm if needed, and use sync controls (a daily background sync also runs).

Useful commands from the same project directory:

```bash
dotnet build    # also runs npm run build
npm run build   # rebuild generated wwwroot/js files only
npm run watch   # watch Scripts/*.ts and rebuild frontend bundles
```

Frontend source lives in **`Scripts/*.ts`**. Do not edit **`wwwroot/js/*.js`** directly; those files are generated.

**Security:** Identity is aimed at trusted LAN use, not hardened for public internet; relax password rules vs production defaults live in **`Program.cs`** — tighten before wider exposure.

**Docker / LAN hosting:** **[HOSTING.md](HOSTING.md)** creates a compose setup in a parent runtime folder, next to the cloned **`DiscogScrobblerMVC/`** repo directory.
