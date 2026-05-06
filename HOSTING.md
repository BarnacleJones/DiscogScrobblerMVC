# Docker on Linux — quick setup

Linux server only. Goal: folder on disk → `docker compose up` → site in browser.

---

## Before you start

You need installed on the server:

- Docker (with Compose v2 — the `docker compose` command, not the old `docker-compose` plugin mess)
- Git

From Ubuntu / Mint-ish:

```bash
sudo apt update
sudo apt install -y docker.io docker-compose-plugin git
sudo usermod -aG docker "$USER"
```

Log out and back in so Docker works without `sudo`.

You also need Discogs keys: **[discogs.com](https://www.discogs.com)** → Profile → **Settings** → **Developers** — create an app, copy **consumer key / secret**, click **Generate token** for your personal token.

(Optional) Last.fm: **[last.fm/api](https://www.last.fm/api)** — API key + secret if you care about scrobbling.

---

## 1 — Make a folder and clone the repo into it

Pick a path. Example: `~/docker/DiscogScrobbler`.

```bash
mkdir -p ~/docker/DiscogScrobbler
cd ~/docker/DiscogScrobbler
git clone https://github.com/BarnacleJones/DiscogScrobblerMVC.git
```

You should see an inner **`DiscogScrobblerMVC/`** project directory when you run `ls`.

---

## 2 — Create runtime folders for data, logs, covers

Still in `~/docker/DiscogScrobbler`:

```bash
mkdir -p data logs images
```

---

## 3 — `appsettings.Local.json`

Still in **`~/docker/DiscogScrobbler`**:

```bash
nano appsettings.Local.json
```

Paste this. Fill in **Discogs** (all three). Fill in **Last.fm** only if you use scrobbling; you can leave those empty and connect Last.fm later in the app **Settings** instead. That is recommended, using them in app settings is untested.

Leave **`App`** exactly as shown — it points covers at **`/app/images`**, which matches the **`./images:/app/images`** line in Compose below.

```json
{
  "Discogs": {
    "ConsumerKey": "",
    "ConsumerSecret": "",
    "PersonalAccessToken": ""
  },
  "LastFm": {
    "ApiKey": "",
    "ApiSecret": "",
    "SessionKey": "",
    "Username": "",
    "Password": ""
  },
  "App": {
    "ImageBasePath": "/app/images"
  }
}
```

Save, exit (`Ctrl+O`, Enter, `Ctrl+X` in nano).

The file must be **valid JSON** (not a blank file). **Discogs** can’t be empty strings or the app will crash on startup — fill all three. Fragile? Maybe. But that integration is sort of the entire point of the app. 

Last.fm can stay empty, use **Settings → Connect Last.fm**.

---

## 4 — Environment file for the container

Still in **`~/docker/DiscogScrobbler`**, create **`.env`**:

```bash
nano .env
```

Paste exactly:

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Data Source=/app/data/app.db
```

Save and exit.

---

## 5 — Compose file

Still in **`~/docker/DiscogScrobbler`**, create **`docker-compose.yml`**:

```bash
nano docker-compose.yml
```

Paste:

```yaml
services:
  discogs-scrobbler:
    build:
      context: .
      dockerfile: DiscogScrobblerMVC/dockerfile
    restart: unless-stopped
    ports:
      - "5100:8080"
    volumes:
      - ./data:/app/data
      - ./logs:/app/logs
      - ./images:/app/images
      - ./appsettings.Local.json:/app/appsettings.Local.json:ro
    env_file:
      - .env
```

Save and exit.

`5100` is the port on **your Linux box** → inside the container the app listens on `8080`. Open **`http://YOUR_SERVER_IP:5100`** from another PC on the LAN.

---

## 6 — Run

```bash
cd ~/docker/DiscogScrobbler
docker compose up -d --build
```

First build can take several minutes.

Check it’s alive:

```bash
docker compose ps
docker compose logs -f
```

After first build has run you can use **up** as normal

```bash
docker compose up -d
```
---

## After it’s running

1. **Browser** → `http://<server-ip>:5100`
2. **Register** a login (your own server; no magic “admin” account).
3. Open **Settings** → set **Discogs username** → run **sync** (or wait for the automatic daily sync). Larege collections will take a while to fully populate data (30 mins maximum maybe for 500 records). Gotta be polite to discogs servers! You can browse an incomplete collection while it syncs.

---

## Update app to latest code later

```bash
cd ~/docker/DiscogScrobbler
git pull
docker compose up -d --build
```
If that isn't working, it may be a breaking change:

```bash
cd ~/docker/DiscogScrobbler
git pull
docker compose up --build --force-recreate
```

Your database and `images/` stay in **`./data`** and **`./images`** — they aren’t wiped by rebuild (because of the mounts).

---

## Optional: copy old cover files

If this app saved covers under **`/tmp/DiscogScrobblerMVC/images/`** during dev, or if you have them saved somewhere from running instances before, you can copy the `*.jpg` files into **`~/docker/DiscogScrobbler/images/`** before or after first run — same filenames, less re-downloading.

---

## Troubleshooting — one glance

| Symptom | Try |
|---------|-----|
| `npm: not found` during image build | You’re on an old checkout — **`git pull`**. Current Dockerfile installs Node on purpose. |
| Build context huge / slow | Repo should include root **`.dockerignore`** — **`git pull`**. |
| Covers disappear after rebuild | You didn’t keep **`./images:/app/images`** or the **`images`** folder isn’t writable. |
| Covers are written to temp instead of `/app/images` | Keep `App:ImageBasePath` set to `/app/images` and ensure the `./images:/app/images` mount exists and is writable. |
| Wrong port | Edit **`docker-compose.yml`** `5100:8080` — left side is yours. |

Lots of logging, so check the logs too.

For local development (not Docker), see **[`LOCAL_DEVELOPMENT.md`](LOCAL_DEVELOPMENT.md)**. Project overview: **[`README.md`](README.md)**.
