# E2E tests (Playwright)

## Quick instructions to run the UI tests locally.

### Prereqs
- Node 18+ and npm
- dotnet 8+/10 runtime (matches project)

### Install

```bash
cd tests/e2e
npm ci
npm run install-playwright
```

### Start services (separate shells) or start both with the combined script:

- Start both backend and frontend (PowerShell):

```powershell
npm run start:all:ps
```

Only the combined start script is provided to ensure the test DB isolation and orchestration.

### Run full CI-style (start services, run headless tests, stop services):

```powershell
npm run run:ci
```

### Run non-headless automated tests (start services, run visible tests, stop services):

```powershell
npm run run:headed
```

Run tests

Headless (CI):

```bash
npm run test
```

Headed (debug):

```bash
npm run test:debug
```

Cleanup

```bash
npm run stop:ps
```

Notes
- Tests use an isolated SQLite DB at `tests/e2e/.data/test.db` by setting `SQLITE_PATH`.
- The backend is started with `ASPNETCORE_ENVIRONMENT=IntegrationTests` to allow test-only behavior if implemented.

# Prereqs
- **Node 18+** and `npm` (for Playwright and dev server)
- **.NET 8+/10 runtime and SDK** (to run the backend with `dotnet run`)
- PowerShell (Windows: PowerShell 5.1 or PowerShell 7+; scripts are compatible with PowerShell 5.1)

## Setup
1. Open a shell in this folder:

```powershell
cd tests\e2e
```
2. Install dependencies and Playwright browsers:

```powershell
npm ci
npm run install-playwright
```

# How to run
- Start services only (separate shells) — start backend and frontend manually:

```powershell
# In one shell: start backend
$env:SQLITE_PATH = "$PWD\.data\test.db"; $env:ASPNETCORE_ENVIRONMENT='IntegrationTests'; dotnet run --project ..\..\backend

# In another shell: start frontend
cd ..\..\frontend; npm run dev
```

- Start both services via the helper script (recommended):

```powershell
# From tests/e2e
npm run start:all
```

- Run CI-style (start services, run headless tests, stop services):

```powershell
npm run run:ci
```

- Run visible (headed) tests:

```powershell
npm run run:headed
```

NPM scripts
- **start:all**: starts backend and frontend using `start-all.ps1`.
- **run:ci**: runs `run-ci.ps1` which starts services, runs headless Playwright tests, then stops services.
- **run:headed**: runs `run-headed.ps1` which starts services, runs visible tests, then stops services.
- **stop**: runs `stop-all.ps1` to stop services started by the scripts.

PowerShell scripts
- `start-all.ps1` ([tests/e2e/start-all.ps1](tests/e2e/start-all.ps1)) — Starts an isolated SQLite DB file in `.data`, launches the backend (`dotnet run`) and the frontend dev server (`npm run dev`) as background processes and writes PIDs and logs to `.data`.
- `stop-all.ps1` ([tests/e2e/stop-all.ps1](tests/e2e/stop-all.ps1)) — Attempts to stop processes started by `start-all.ps1` using recorded PIDs and cleans up temp files.
- `run-ci.ps1` ([tests/e2e/run-ci.ps1](tests/e2e/run-ci.ps1)) — Convenience script that calls `start-all.ps1`, runs Playwright tests in headless mode, then calls `stop-all.ps1`.
- `run-headed.ps1` ([tests/e2e/run-headed.ps1](tests/e2e/run-headed.ps1)) — Same as `run-ci.ps1` but runs tests in headed/visible mode for debugging.

Logs, DB and artifacts
- Logs and PIDs are created under `.data/` (e.g., `.data/backend.out.log`, `.data/frontend.err.log`, `.data/backend.pid`).
- The test SQLite DB is at `.data/test.db` — the scripts remove and recreate it to ensure a clean seeded state for tests.

Troubleshooting
- If the frontend process fails to start with `Start-Process` on Windows, ensure `npm` is accessible on `PATH`. The scripts prefer `npm.cmd` or run via `cmd.exe` when needed.
- If PowerShell blocks script execution, run via `npm run start:all` which uses `-ExecutionPolicy Bypass`, or run directly with:

```powershell
powershell -ExecutionPolicy Bypass -File start-all.ps1
```

- Check `.data/backend.err.log` and `.data/frontend.err.log` for errors from each service.
- Ensure `dotnet` and `npm` versions match requirements:

```powershell
Get-Command dotnet; dotnet --version
Get-Command npm; npm --version
```

- If ports are already in use (default backend at `http://localhost:5000`), stop the conflicting process or change `ASPNETCORE_URLS` in the script.
