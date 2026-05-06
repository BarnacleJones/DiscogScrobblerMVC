# DiscogScrobblerMVC

**Run it:** 

**[Docker / production-style hosting](HOSTING.md)**

**[Local development](LOCAL_DEVELOPMENT.md)**

Personal Discogs ↔ Last.fm companion: mirror your vinyl collection locally, browse it, and **scrobble a whole release** to Last.fm when a side/session ends. Built for **self-hosting** on a home LAN (not a hardened public SaaS).

**What it does:** Discogs-backed collection sync (metadata, tracklists, art to local disk), searchable collection UI, artist/label pages scoped to owned releases, **Tracks** browser, **Stats dashboard** (`/Stats`), **Settings** (Discogs username, sync triggers, Last.fm OAuth), periodic + on-demand sync jobs, and Serilog logs.

Stack: ASP.NET Core 8 MVC + Identity, SQLite / EF Core, TypeScript → esbuild, Bootstrap + DataTables.


## Build notes

`dotnet build` runs `npm run build`, which bundles the TypeScript entry points into `wwwroot/js`. Run `npm ci` first on a fresh checkout.

AI disclaimer - Agentic AI was used under my supervision. This is a work in progress and tidying is ongoing.
