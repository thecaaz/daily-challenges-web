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
