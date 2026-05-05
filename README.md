# DiscogScrobblerMVC

A personal tool for browsing a Discogs vinyl collection and scrobbling plays to Last.fm. Intended for self-hosting on a local home network.

## Purpose

The intended workflow is to sync a Discogs collection into the app, browse releases, and scrobble tracks to Last.fm when listening on vinyl. It is not designed for public access.

## Current functionality

- Discogs collection sync via background service (releases, artists, labels, tracklists, cover art)
- Release detail pages with tracklist, genres, styles, and community stats (have/want counts from Discogs)
- Artist and label pages showing owned releases
- Collection browser with filtering and pagination
- Cover image download and local storage

Last.fm scrobbling is partially integrated. The infrastructure is in place but the scrobble calls are not yet wired up.

## Technical overview

- **Type:** ASP.NET Core 8 MVC
- **Database:** SQLite, single `app.db` file, Entity Framework Core 8 code-first migrations
- **Auth:** ASP.NET Core Identity with Discogs personal access token for API access
- **Background jobs:** `DiscogsBackgroundService` runs collection sync, release detail fetch, and image download on startup
- **Logging:** Serilog rolling file logs (14-day retention)
- **Frontend:** TypeScript compiled with esbuild, Bootstrap, DataTables.net

API credentials (Discogs consumer key/secret and personal access token) are configured via `appsettings.Local.json`, which is not checked in.

## Hosting

Docker: TODO

The app is intended to run as a Docker Compose service on a local network. Docker configuration has not been set up yet.

## Roadmap

1. Last.fm scrobble integration
2. Docker Compose setup for local network deployment
3. Data visualisations (listening history, collection statistics)
4. Deeper collection exploration features
