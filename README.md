# Daily Challenges — MVP

This repository contains a minimal full-stack MVP for submitting daily challenge scores.

Backend (C# / ASP.NET Core)
- Folder: `backend`
- Uses EF Core with SQLite (app.db) and serves uploaded images from `wwwroot`.

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