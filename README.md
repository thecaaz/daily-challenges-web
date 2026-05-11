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
docker compose up --detach --build
```

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

