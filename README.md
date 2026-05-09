# DiscogScrobblerMVC

**Run it:** 

**[Docker / production-style hosting](HOSTING.md)**

**[Local development](LOCAL_DEVELOPMENT.md)**

Personal Discogs ↔ Last.fm companion: mirror your vinyl collection locally, browse it, and **scrobble a whole release** to Last.fm when a side/session ends. Built for **self-hosting** on a home LAN (not a hardened public SaaS).

**Security posture:** The app targets **trusted LAN / hobby self-hosting**. Identity password rules are intentionally relaxed (see **`Program.cs`**). Each user’s **Discogs personal access token** and **Last.fm session key** (after OAuth) are stored in SQLite **as plain text** so the code stays simple; anyone who obtains the database file can use those credentials on the respective services. That is **not** appropriate for public internet hosting or sensitive data without redesign (encryption at rest, secret management, tighter auth, and likely a different threat model). If you ever move beyond a home lab, plan to **tighten** these areas first.

**Accounts:** The first person to **register** on a new database becomes **Admin** and can sign in immediately. There is **no** email confirmation. Everyone after that registers as **pending** until an Admin **approves** them under **Settings** (or **denies**, which removes the account record). Pending users cannot log in. While requests are waiting, the **Settings** link in the navbar can show a small **badge** (counts pending registrations; refreshed on each page load).

**What it does:** Discogs-backed collection sync (metadata, tracklists, art to local disk), searchable collection UI, artist/label pages scoped to owned releases, **Tracks** browser, **Stats dashboard** (`/Stats`), **Settings** (Discogs username, optional personal access token, sync triggers, Last.fm OAuth), periodic + on-demand sync jobs, and Serilog logs.

**Discogs token:** Optional only if your Discogs **collection** is **public** (account privacy / collection visibility). Without a token the app syncs folder **0** ("All") anonymously and **cannot** list your folders (that API is owner-only). **Private** collections need **Generate token** (Discogs → Settings → Developers) saved in **Settings** here, or Discogs returns **403**. **Collection value** (min/median/max) **always** needs a token.

**Cover art on disk (`App:ImageBasePath`):** The app caches images under one configurable folder (see **[HOSTING.md](HOSTING.md)** for Docker paths). Inside that folder it uses:

| Path under `ImageBasePath` | Contents |
|---|---|
| `{discogs username}/` | Release cover JPGs for each synced account. The name is sanitized for the filesystem (invalid characters rewritten). **`artists` and `labels` are reserved** — those Discogs usernames cannot be turned into folders; use a slightly different spelling on Discogs or cover art URLs fall back to remote Discogs for that account until you pick a workable username. |
| `artists/` | Shared artist profile images (`artist-{id}.jpg`, thumbnails beside them). |
| `labels/` | Shared label profile images (`label-{id}.jpg`, thumbnails beside them). |

HTTP mappings match the disk layout (`/images/…` with the same subfolders).


Stack: ASP.NET Core 8 MVC + Identity, SQLite / EF Core, TypeScript → esbuild, Bootstrap + DataTables.


## Build notes

`dotnet build` runs `npm run build`, which bundles the TypeScript entry points into `wwwroot/js`. Run `npm ci` first on a fresh checkout.

AI disclaimer - Agentic AI was used under my supervision. This is a work in progress and tidying is ongoing.
