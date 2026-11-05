# Daily Challenges

[![Release](https://img.shields.io/github/v/release/thecaaz/daily-challenges-web?style=flat-square)](https://github.com/thecaaz/daily-challenges-web/releases)
[![CI](https://img.shields.io/github/actions/workflow/status/thecaaz/daily-challenges-web/ci.yml?branch=main&label=CI&logo=github&style=flat-square)](https://github.com/thecaaz/daily-challenges-web/actions/workflows/ci.yml)
[![Backend](https://img.shields.io/docker/v/caaz/daily-challenge-web-backend?sort=semver&label=backend&logo=docker&style=flat-square)](https://hub.docker.com/r/caaz/daily-challenge-web-backend)
[![Frontend](https://img.shields.io/docker/v/caaz/daily-challenge-web-frontend?sort=semver&label=frontend&logo=docker&style=flat-square)](https://hub.docker.com/r/caaz/daily-challenge-web-frontend)
[![Repo size](https://img.shields.io/github/repo-size/thecaaz/daily-challenges-web?style=flat-square)](https://github.com/thecaaz/daily-challenges-web)
[![Top language](https://img.shields.io/github/languages/top/thecaaz/daily-challenges-web?style=flat-square)](https://github.com/thecaaz/daily-challenges-web)

A full-stack web application for submitting and tracking daily challenge scores with leaderboards, personal history, and an XP/level system.

## Features

- **Submit scores** for daily challenges, with optional screenshot uploads
- **Leaderboards** — per-game highscores and personal highscore history
- **XP and levels** — earn XP for each submission; level up as you accumulate points
- **Submission detail** — view individual submission scores and screenshots
- **Admin panel** — create/edit/delete games, manage and score submissions with date filtering
- **Admin user management** — manage users, audit xp, add/remove xp
- **Authentication** — register and log in with username/password
- **Profile** - show last active date, show active games, submission counts, best scores

## Stack

Backend (C# / ASP.NET Core)
- Folder: `backend`
- Uses EF Core with SQLite (app.db)

Frontend (React + Vite)
- Folder: `frontend`
- Uses Material UI.

Run (development)

1. Backend: open a terminal and run:

```
dotnet run --project backend
```

2. Frontend: in another terminal:

```
cd frontend
npm install
npm run dev
```

Open the VS Code debugger and run the "Run Full Stack" compound configuration to start both.

**Docker**

- **Build images (first time or after changes):**

```bash
docker compose build
```

- **Start services with the compose file (mounts SQLite at /data/app.db):**

```bash
export JWT_KEY="$(openssl rand -base64 48)"   # required; keep it stable across restarts
docker compose up --detach --build
```

- **Upgrading an existing deployment:** the backend container now runs as a non-root user (uid 1654), so a database volume created by an older image must be handed over once before starting the new image:

```bash
docker run --rm -v daily-challenges-web_sqlite_data:/data alpine chown -R 1654:1654 /data
```

- The frontend container now listens on port 8080 inside the container (compose maps it to 5173), and the SPA calls the API on the same origin through nginx (`BACKEND_URL` empty). The backend port 5000 is only published on `127.0.0.1`; put your public reverse proxy in front of the frontend container, not the backend.

- **Client IPs (rate limiting):** the request chain is `client → optional TLS proxy (on a private network) → frontend nginx → backend`. The frontend nginx trusts `X-Forwarded-For` only from private/loopback addresses (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `127.0.0.1`, see `set_real_ip_from` in [frontend/nginx.conf](frontend/nginx.conf)), resolves the real client IP from it, and forwards that single address to the backend as `X-Forwarded-For`. The backend trusts exactly one hop (`ForwardLimit=1`, the nginx container) and uses that IP for its per-IP auth rate limiter. Your TLS proxy must therefore reach the frontend container from a private address and append the client IP to `X-Forwarded-For` (most proxies do by default). If it connects from a public address, add that address as another `set_real_ip_from`; without it, every request appears to come from the proxy and all clients share one rate-limit bucket.

- `JWT_KEY` signs login tokens. The backend refuses to start in Production without a strong key (≥ 32 chars). Changing it logs everyone out. Locally, `dotnet run --project backend` uses the Development profile, which has a dev-only key in `appsettings.Development.json`.

- The backend listens on port `5000` and uses the environment variable `SQLITE_PATH` to locate the database file. The compose file mounts a volume at `/data` and sets `SQLITE_PATH=/data/app.db` so the SQLite file is persisted in the named volume `sqlite_data`.

- To use a host folder for the DB instead of a named volume, run (from repo root):

```bash
docker compose down
docker compose up --build -d
```

## Migrations

Database schema changes are managed with Entity Framework Core migrations. There are two convenient ways to create and apply migrations.

- From the repository root (explicit project paths):

```bash
dotnet ef migrations add <Name> --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj
dotnet ef database update --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj
```

The migration files are created under [backend/Migrations](backend/Migrations). After running `migrations add`, review the generated migration before applying it.

