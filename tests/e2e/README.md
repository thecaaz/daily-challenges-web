# E2E tests (Playwright)

Quick instructions to run the UI tests locally.

Prereqs
- Node 18+ and npm
- dotnet 8+/10 runtime (matches project)

Install

```bash
cd tests/e2e
npm ci
npm run install-playwright
```

Start services (separate shells) or start both with the combined script:

- Start both backend and frontend (PowerShell):

```powershell
npm run start:all:ps
```

Only the combined start script is provided to ensure the test DB isolation and orchestration.

Run full CI-style (start services, run headless tests, stop services):

```powershell
npm run run:ci
```

Run non-headless automated tests (start services, run visible tests, stop services):

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
