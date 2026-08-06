# Daily Challenges — Agent Instructions

## Stack

- **Backend**: C# / ASP.NET Core 10.0, EF Core 7 with SQLite, Swagger in dev
- **Frontend**: React 18 + Vite + Material UI (JS, not TS)
- **Solution file**: `daily-challenges-web.sln` at repo root

## Run locally

```bash
# Backend
dotnet run --project backend

# Frontend (separate terminal)
cd frontend && npm install && npm run dev
```

VS Code "Run Full Stack" compound config launches both.

## Build & test

```bash
# Backend build
dotnet build daily-challenges-web.sln

# Backend unit tests
dotnet test tests/backend

# Backend integration tests (uses real SQLite)
dotnet test tests/integration

# Frontend build
cd frontend && npm run build

# Frontend tests
cd frontend && npm test
```

## Migrations (EF Core)

Requires the global tool `dotnet-ef` (declared in `dotnet-tools.json`).

```bash
dotnet ef migrations add <Name> --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj
dotnet ef database update --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj
```

Migration files live in `backend/Migrations/`. Review generated migration before applying.

## Docker

```bash
docker compose build          # first time or after changes
docker compose up --detach    # start services
```

Backend listens on `:5000`, sets `SQLITE_PATH` env var to locate the DB. A named volume `sqlite_data` persists the SQLite file.

## Architecture pointers

- `backend/Program.cs` — DI registration, auth setup, middleware pipeline, DB migration on startup
- `backend/Controllers/` — API endpoints
- `backend/Services/` — business logic (interfaces in `Services/Contracts/`)
- `backend/Repositories/` — data access (Ef* implementations)
- `backend/Data/AppDbContext.cs` — EF context
- `backend/Migrations/` — EF migration files
- `backend/Middleware/` — custom middleware (e.g., exception handling)
- `frontend/src/main.jsx` — React entry point
- `frontend/src/App.jsx` — routing
- `frontend/src/api.js` — HTTP client (axios)
- `frontend/src/contexts/` — React contexts (Auth, Snackbar, Theme)
- `frontend/src/hooks/` — custom hooks
- `frontend/src/pages/` — route components
- `frontend/src/components/` — shared UI components
- `frontend/src/styles/tokens.js` — design tokens (colors, spacing)

## Testing notes

- Backend unit tests: `tests/backend/` — xUnit, Moq
- Backend integration tests: `tests/integration/` — xUnit, real SQLite via EF in-memory
- Frontend tests: Vitest with `happy-dom` (config in `vite.config.js`), setup in `frontend/src/test/setup.js`

## Constraints & conventions

- Commits follow conventional commits (enforced by semantic-release on `main`)
- Releases use semantic-release (`npm run release`), tags `v<major>.<minor>.<patch>`
- No secrets in repo — JWT key, Docker Hub credentials, etc. come from env/secrets
- `backend/appsettings.json` may contain dev-only defaults; production config is external
- `.github/agents/` contains custom Copilot agent definitions (C# backend, frontend UX)
- `.github/COPILOT_POLICY.md` — no cloud Copilot runs without explicit approval
