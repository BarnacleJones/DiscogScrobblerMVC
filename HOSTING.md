# Docker on Linux — quick setup

**Who this is for:** You have a Linux PC or small server at home and want a **single folder on disk** that holds config + data. You run one command and open the app in a browser.

**What you’re doing (big picture):** You’ll create a **home for the app** (config files, database, album art). You’ll **download the source code** with Git into a subfolder. **Docker** then builds a **container** (a boxed-up copy of the app) and runs it. You don’t need to install .NET or Node on the host—Docker handles that **inside** the container.

---

## Before you start

Install on the server:

- **Docker** — includes **Compose** so you can use the `docker compose` command (this guide uses that modern style).
- **Git** — downloads the project from GitHub.

On Ubuntu / Mint-style distros you can use:

```bash
sudo apt update
sudo apt install -y docker.io docker-compose-plugin git
sudo usermod -aG docker "$USER"
```

**Important:** Log out and log back in (or reboot) after `usermod` so your user can run `docker` without typing `sudo` every time.

**Discogs (required for the app to start):** On **[discogs.com](https://www.discogs.com)** go to Profile → **Settings** → **Developers**. Create an app and copy the **consumer key** and **consumer secret**.  
*(Those two keys go into a config file below. Each person who uses your server still pastes their own **personal token** (“Generate token”) in the app’s **Settings** after they log in—that’s separate.)*

**Last.fm (optional):** If you want scrobbling, create an API app on **[last.fm/api](https://www.last.fm/api)** and note the **API key** and **API secret**.

---

## 1 — Make a folder and download the code

Pick where everything will live. This guide uses:

`~/docker/DiscogScrobbler`

Create a shell and run:

```bash
mkdir -p ~/docker/DiscogScrobbler
cd ~/docker/DiscogScrobbler
git clone https://github.com/BarnacleJones/DiscogScrobblerMVC.git
```

Check what you have:

```bash
ls
```

You should see a folder named **`DiscogScrobblerMVC`**. That folder is the **GitHub project** (source code). The **next steps add files beside it**, in the **parent** folder `~/docker/DiscogScrobbler`.

**Mental picture** (you’ll have this layout after a few steps):

```text
~/docker/DiscogScrobbler/              ← “compose folder”: you run docker compose HERE
  docker-compose.yml                 ← you will create (step 6)
  appsettings.Local.json             ← you will create (step 3)
  .env                               ← you will create (step 4)
  .dockerignore                      ← optional but recommended (step 5)
  data/   logs/   images/            ← you will create (step 2)
  DiscogScrobblerMVC/                ← already here after git clone
      dockerfile                     ← Docker reads this when building
      … (rest of the project) …
```

---

## 2 — Folders for data, logs, and cover images

Stay in **`~/docker/DiscogScrobbler`** (the **parent** folder, not inside `DiscogScrobblerMVC` unless the instructions say so).

```bash
cd ~/docker/DiscogScrobbler
mkdir -p data logs images
```

- **`data`** — SQLite database file will live here (mounted to **`/app/data`** in the container).
- **`logs`** — optional host-side log mount (mapped to **`/app/logs`** in the container).
- **`images`** — cached album/artist/label pictures (mounted to **`/app/images`**). Under that, the app creates:
  - One subfolder per **Discogs username** (sanitized for the filesystem).  
    Exception: usernames **`artists`** or **`labels`** (any capitalization) are **reserved** and won’t get a folder—they’d clash with shared catalog folders.
  - **`artists/`** and **`labels/`** for shared profile images.

If someone never sets a Discogs username in the app, release art may come straight from Discogs URLs until they do.

---

## 3 — `appsettings.Local.json`

Still in **`~/docker/DiscogScrobbler`**:

```bash
nano appsettings.Local.json
```

Paste the template below. Fill in **Discogs** `ConsumerKey` and `ConsumerSecret` from your Discogs developer app.  
For **Last.fm**, fill `ApiKey` and `ApiSecret` only if you use scrobbling; every user still clicks **Connect Last.fm** in the app so their own account is linked.

Leave **`App:ImageBasePath`** as **`/app/images`** — that matches the Docker volume line you’ll paste in step 6.

```json
{
  "Discogs": {
    "ConsumerKey": "",
    "ConsumerSecret": ""
  },
  "LastFm": {
    "ApiKey": "",
    "ApiSecret": ""
  },
  "App": {
    "ImageBasePath": "/app/images"
  }
}
```

In **nano**: save with **Ctrl+O**, Enter, then exit with **Ctrl+X**. (You can use any editor you like instead of nano.)

**JSON must be valid** — at minimum real `{}` with quotes, not an empty file. **Discogs key and secret cannot be empty** or the app will exit on startup. The **personal Discogs token** is **not** this file; each user adds that in **Settings** after logging in.

**Last.fm:** Leave both empty if you don’t care about scrobbling. If you set them, all users share that **application** key pair; each still links **their** Last.fm account in **Settings**.

---

## 4 — Environment file for the container

Still in **`~/docker/DiscogScrobbler`**:

```bash
nano .env
```

Paste **exactly**:

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Data Source=/app/data/app.db
```

Save and close.

---

## 5 — `.dockerignore` (recommended — speeds up builds)

**In plain English:** When Docker **builds** the image, it packs up everything under **`~/docker/DiscogScrobbler`** and sends it to the Docker engine. Without a ignore-list, it might include huge folders (old builds, `node_modules`, your real database copy, etc.) and builds feel **slow** or use a lot of disk.

A **`.dockerignore`** file in **`~/docker/DiscogScrobbler`** (same place as **`docker-compose.yml`** will go) tells Docker **what to skip**.  
**Git clone does not create this file for you** — create it once yourself.

```bash
cd ~/docker/DiscogScrobbler
nano .dockerignore
```

Paste:

```gitignore
**/[Bb]in/
**/[Oo]bj/
**/node_modules/
.git/
.idea/
.vs/
**/*.user
**/logs/
**/*.db
**/*.db-shm
**/*.db-wal
data/
images/
.env
appsettings.Local.json
```

Save and close. If you later put other giant folders next to the project, you can add more lines to ignore them too.

---

## 6 — Compose file

Still in **`~/docker/DiscogScrobbler`**:

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

Save and close.

- **`5100`** — port on **your Linux machine** (change if something else uses it).
- **`8080`** — port **inside** the container (leave this side alone unless you know you need to change it).

Open **`http://YOUR_SERVER_IP:5100`** from another device on your home network.

---

## 7 — Build and run

```bash
cd ~/docker/DiscogScrobbler
docker compose up -d --build
```

- **`--build`** — rebuild image if the code or Dockerfile changed.
- **`-d`** — **detached** — runs in the background so your terminal is free.

The **first** build can take several minutes.

**Check that it’s running:**

```bash
docker compose ps
docker compose logs -f
```

(`logs -f` streams logs; **Ctrl+C** stops following—the container keeps running.)

After the first successful build, day-to-day you can often do:

```bash
cd ~/docker/DiscogScrobbler
docker compose up -d
```

---

## After it’s running

The database under **`./data`** stores each user’s Discogs **personal token in plain text** (by design for a simple home app). **Anyone who copies that file can use those tokens on Discogs.** Read the **Security posture** section in **[README.md](README.md)** before putting this on the public internet.

1. Browser → **`http://<server-ip>:5100`**
2. **Register** the first user — they become **Admin** and can sign in immediately.
3. Anyone else: **Register**, then wait. They **cannot log in** until an Admin opens **Settings** and **approves** them (or **denies**, which removes that signup). There is **no** email confirmation.
4. In **Settings**, set at least **Discogs username**. Add **personal access token** if the Discogs collection is private or you want **collection value** synced — without a token, collection sync only works if the Discogs **collection visibility** is public (see **[README.md](README.md)**). Then sync (or wait for the daily sync). Large collections can take a long time—Discogs rate limits are normal. You can use the site while sync runs.

---

## Updating to newer code later

You have **two** folder levels that matter:

| Folder | Role |
|--------|------|
| **`~/docker/DiscogScrobbler`** | Where **`docker compose`** runs. Holds your data volumes and config. |
| **`~/docker/DiscogScrobbler/DiscogScrobblerMVC`** | The **Git** repo. **`git pull`** belongs **here**. |

**Routine update:**

```bash
cd ~/docker/DiscogScrobbler/DiscogScrobblerMVC
git pull
cd ..
docker compose up -d --build
```

If something big changed and a normal rebuild isn’t enough:

```bash
cd ~/docker/DiscogScrobbler/DiscogScrobblerMVC
git pull
cd ..
docker compose up --build --force-recreate
```

Your **database** and **images** stay in **`./data`** and **`./images`** on the host—rebuilding the container does not delete those because of the **volume** mounts.

---

## Troubleshooting — quick table

| What you see | What to try |
|--------------|-------------|
| **`npm: not found`** during image build | In **`~/docker/DiscogScrobbler/DiscogScrobblerMVC`** run **`git pull`** so you get the current Dockerfile (it installs Node on purpose). Then rebuild from the parent folder. |
| Build **very slow** or huge upload | Add **`.dockerignore`** next to **`docker-compose.yml`** — see **step 5**. |
| **Covers gone** after a rebuild | Check **`./images:/app/images`** is still in Compose and the **`images`** folder is writable. |
| Covers not landing in **`/app/images`** | Keep **`App:ImageBasePath`** as **`/app/images`**, keep the volume line, read logs for permission errors. |
| Collection never updates, or log shows 403 / &quot;authenticate as the owner&quot; | Set **Discogs username**. If no **personal token**: Discogs **collection** must be **public** in Discogs privacy settings. Private collections need **Generate token** in **Settings**. **Collection value** (min/median/max) always needs a token. |
| **Wrong port** | In **`docker-compose.yml`**, change **`5100:8080`** — change **only the first number** (`5100`) unless you know you need to change the container side. |

If in doubt, **`docker compose logs`** usually explains a startup failure.

---

**Not using Docker?** See **[LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md)**. **Project overview:** **[README.md](README.md)**.
