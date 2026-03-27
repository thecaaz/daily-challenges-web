# E2E UI-only test plan and exhaustive UI scenarios

This document focuses exclusively on UI-driven end-to-end tests: tests must exercise functionality only via the web UI (no direct API calls). All API endpoints should be exercised as part of user interactions in the SPA.

Guiding rules

- Tests must use UI interactions (navigation, form fills, clicks, file-pickers) and not call backend endpoints directly.
- Prefer semantic selectors (`getByRole`, `getByLabel`, visible text). Use `tests/e2e/test-utils.ts` helpers for login and `createGame` flows when the helper itself uses UI interactions.
- Where a backend-only admin view exists in the UI, the test must navigate to that admin UI and perform actions there.
 
## Medium priority UI scenarios

- Pagination, available dates, and page metadata in UI
  - Flow: Create many submissions using the UI across different dates → Visit game's submissions page → Use UI pagination controls / date filters → Verify that items change and UI shows `page`, `total pages`, `has more` or date filter options as expected.

## Low priority / Nice-to-have UI scenarios

- Auth flows via UI: register, login, logout, and profile/me
  - Flow: Use the UI register form to create a new user → Login via UI → Verify protected pages accessible → Use UI logout control and verify redirected and protected pages inaccessible. Verify the UI reflects admin vs normal user features (Admin link hidden for normal users).

- Invalid file uploads via UI
  - Flow: Attempt to attach unsupported file types or oversize files in game create/edit and submission screens → Submit and verify the UI surfaces validation errors and prevents the action.

- Edit and delete flows in admin UI - full coverage
  - Flow: Cover all admin UI buttons: edit game, delete game, view submissions, purge/reset where present. Verify UI states after each action (confirmations, lists updated).

## Implementation notes and strategies

- Use `tests/e2e/test-utils.ts` helpers where they perform UI interactions (e.g., `login(page)` uses the UI login). If a helper calls the backend directly, rewrite or extend it to perform UI login instead.
- Use Playwright's `setInputFiles` to attach images/screenshots in file inputs. Store test assets under `tests/e2e/fixtures/` (create the folder) for reuse.
- For timing-sensitive behavior (scoring day boundaries), prefer setting up test data using UI flows that create submissions at different timestamps where possible; otherwise, use long-lived tests that simulate multiple days by manipulating the UI's visible date filters to assert behavior.
- Assert user-visible messages and UI state rather than backend response codes.