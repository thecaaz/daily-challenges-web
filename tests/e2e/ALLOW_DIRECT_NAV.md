# Allowed direct navigation exceptions for e2e tests

This file documents the minimal, reviewed exceptions where tests use direct URL navigation (`page.goto(...)`) because the UI cannot reliably reach the required state.

Rules
- Keep exceptions minimal and well-documented.
- Prefer UI-only navigation and semantic selectors (`getByRole`) in new tests.
- When adding a new exception, document the reason and consider adding a frontend improvement instead.

Current exceptions

- `tests/e2e/tests/duplicate-submission.spec.ts`
  - Why: After a successful first submit the public submissions page disables the "Submit Score" control for the same user / scoring-day (server-side `hasSubmittedForLatest`). To reliably reproduce the duplicate-submission server-side handling we navigate directly to `/submit/{gameId}` for the second attempt. The first submission flow still uses the UI-only helpers.

- `tests/e2e/tests/admin-edit-delete-submission.spec.ts`
  - Why: The test verifies that a deleted submission's public detail returns "Not found". The `openSubmissionById` helper prefers a UI link click but falls back to `page.goto('/submission/{id}')` when no UI link is available (documented fallback). This is a narrow, understandable exception for asserting a server-side 404.

Notes
- These are intended to be temporary and evaluated periodically. If frontend changes can enable a UI-only path for these cases, prefer implementing that and removing the exception.
