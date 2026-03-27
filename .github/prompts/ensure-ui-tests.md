---
title: Ensure UI tests pass (Playwright e2e)
description: |
  Run, diagnose, and fix Playwright UI (e2e) tests for this repository. This prompt
  guides an agent to run the provided CI script (`tests/e2e/run-ci.ps1`), inspect
  failing test context in `tests/e2e/test-results`, search the frontend for UI
  elements, and apply minimal, UI-focused fixes to make the tests green.
scope: workspace
inputs:
  mode:
    description: "Operation mode: `quick` (only re-run failing tests), `full` (run full CI), or `fix-only` (attempt fixes without full verification). Defaults to `full`."
    required: false
  runTests:
    description: "Boolean. Whether to execute `tests/e2e/run-ci.ps1` to validate tests. Defaults to true."
    required: false
---

Purpose
- Ensure all UI (Playwright) e2e tests in `tests/e2e` run green in this repository.
- If tests fail, diagnose using `tests/e2e/test-results` (error-context.md, screenshots, videos) and implement minimal, targeted fixes.
- Prefer UI-only interactions for navigation and assertions (avoid brittle direct-URL or internal-API navigation unless required for reliable verification).

Context & quick references
- Primary CI/run script: `tests/e2e/run-ci.ps1` (starts backend/frontend and runs headless Playwright).
- Playwright tests: `tests/e2e/tests/*.spec.ts`
- Shared helpers: `tests/e2e/test-utils.ts` and `tests/e2e/test-data/`
- Failure artifacts: `tests/e2e/test-results/<test-name>/` (contains `error-context.md`, screenshots, video.webm).
- Frontend UI: `frontend/src/` (pages `Games.jsx`, `GameHighscore.jsx`, `SubmissionDetail.jsx`, `components/SubmissionCard.jsx`, `pages/Admin.jsx`).

Preconditions
- Development host has Node, npm, .NET SDK available.
- From repo root the CI script can be executed in PowerShell: `tests/e2e/run-ci.ps1`.

Recommended workflow (step-by-step)
1. Run the CI to get an authoritative failing report (mode controls scope):

   - Full CI (headless):

     ```powershell
     tests\e2e\run-ci.ps1
     ```

   - If running locally in non-headless for debugging, set `HEADLESS=false` and run Playwright directly inside `tests/e2e`:

     ```powershell
     cd tests\e2e
     $env:HEADLESS="false"; npx playwright test tests/e2e/tests/<file>.spec.ts -g "<test name>"
     ```

2. For each failing test, inspect `tests/e2e/test-results/<test>/error-context.md` and its screenshot to identify the failing selector/interaction and the DOM snapshot.

3. Open the failing test file and `tests/e2e/test-utils.ts`. Prefer to fix tests/helpers rather than changing application code.

4. Common, minimal fix patterns (try in this order):
   - Replace brittle CSS/text selectors with semantic selectors (e.g., `page.getByRole('textbox', { name: 'Score' })`, `page.getByRole('button', { name: 'Submit' })`).
   - Scope selectors to a specific container (use `locator('strong', { hasText: gameName }).locator('..')` or ancestor xpath) to avoid global matches.
   - Add explicit waits tied to network events: `page.waitForResponse(r => r.url().endsWith('/api/games') && r.request().method() === 'POST')` when a POST creates a resource.
   - Use `await locator.scrollIntoViewIfNeeded()` + `click({ timeout: 10000 })` and fallback to `elementHandle` + `evaluate(el => el.click())` for flaky clicks.
   - For native dialogs use `page.once('dialog', async dialog => { await dialog.accept(); })` and assert `dialog.message()` when relevant.
   - For file upload tests, use `setInputFiles()` with resolved paths: `path.resolve(__dirname, '../test-data/test-image.png')`.
   - Prefer UI-only navigation: find and click the public link for a created game rather than `page.goto('/games/{id}')`.
   - If timing is the root cause, increase targeted timeouts or poll the API from the test ONLY when needed, but prefer UI rendering as the signal.

5. After a local fix, re-run the full CI (`tests/e2e/run-ci.ps1`) to ensure no other regressions.

6. When you make code changes to tests or helpers:
   - If changes have occured and the branch is in `dev`, create a new branch that is descriptive
   - Keep changes minimal and well-justified in the commit message: e.g. `e2e: fix flaky selector in highscore test`.
   - Run the full suite before pushing.
   - If pushChanges=true, commit, push to branch, and open a PR with the failing artifacts and a summary of fixes.

Failure triage tips
- If page snapshot shows element present but not visible, try `scrollIntoViewIfNeeded()` and ensure the element is not covered by a loader or overlay.
- If cookies/auth state differs between test runs, ensure tests log in via UI and avoid reusing server-side tokens.
- If tests rely on server-side dates/scoring-day, inspect backend controllers in `backend/Controllers` to understand constraints and adapt the test to comply (e.g., submit once per scoring day).

Outputs
- Primary: All Playwright UI tests pass (`npx playwright test` / `tests/e2e/run-ci.ps1` exits 0).
- Secondary: A short changelog (files changed, one-line rationale per change) and a summary added to `tests/e2e/todo.md` or the PR description.

Examples
- Full verification (default):
  - Input: `{ "mode": "full", "runTests": true, "pushChanges": false }`
  - Action: runs `tests/e2e/run-ci.ps1`, fixes failures, re-runs CI, returns green signal and a summary.

- Quick iteration on a known failing test:
  - Input: `{ "mode": "quick", "runTests": true }`
  - Action: run the CI to gather the failing test file(s), run specific test(s) headed, apply a minimal fix, re-run the single test and then the CI if green.

Clarifying questions (ask if ambiguous)
- Should fixes be pushed automatically to the current branch, or only suggested as patches for a human reviewer?
- Prefer `UI-only` navigation always, or allow API-assisted polling when UI signal is unreliable?