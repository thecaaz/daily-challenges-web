# E2E UI-only test plan and exhaustive UI scenarios

This document focuses exclusively on UI-driven end-to-end tests: tests must exercise functionality only via the web UI (no direct API calls). All API endpoints should be exercised as part of user interactions in the SPA.

Guiding rules

- Tests must use UI interactions (navigation, form fills, clicks, file-pickers) and not call backend endpoints directly.
- Prefer semantic selectors (`getByRole`, `getByLabel`, visible text). Use `tests/e2e/test-utils.ts` helpers for login and `createGame` flows when the helper itself uses UI interactions.
- Where a backend-only admin view exists in the UI, the test must navigate to that admin UI and perform actions there.

-## High priority UI scenarios (must implement first)

- Admin view: unfiltered submissions through admin UI
  - Flow: Login as admin → Navigate to the admin submissions/unfiltered UI (the page or control that shows all submissions) → Create multiple submissions across dates and with and without user association → Verify the admin UI lists all entries including today's ones.

- Admin updates and deletes a game via admin UI
  - Flow: Login as admin → `/admin` → Create a game → Use the admin edit UI to change `name`, `url`, `resetTime`, `resetTimezone` and save → Verify changes in the UI and on the game's public page. Then delete via admin UI and confirm it's removed from the home list.

- Admin updates and deletes a submission via UI
  - Flow: Login as admin → Navigate to submission management UI → Edit a submission's score and save → Verify change appears on the game's page. Delete a submission and verify it's removed from public listings.

## Medium priority UI scenarios

- Highscore display and personal highscore via UI
  - Flow: Create multiple submissions (different users/timestamps) using UI flows → Visit the game's leaderboard/highscore UI → Verify ordering and top N are displayed correctly. As a logged-in user, view personal highscore area of the UI and assert correct personal top value.

- Duplicate submission attempt via UI (conflict messaging)
  - Flow: Login as the same user → Submit a score for today's scoring day via the UI → Attempt to submit again via UI → Verify the UI shows an error/notification indicating duplicate submission is not allowed (message text asserted).

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

## Suggested next steps

1. Map each backend endpoint to the corresponding UI flow and mark where a test already exists. (task: `Map backend endpoints to UI flows`)
2. Implement fixtures folder `tests/e2e/fixtures/` containing `small-image.png`, `small-screenshot.png`, and one invalid file type to use in attachments.
3. Scaffold Playwright test files (one per scenario group) that use only UI interactions:
   - `create-game-with-image.spec.ts`
   - `submit-with-screenshot.spec.ts`
   - `admin-unfiltered-submissions.spec.ts`
   - `admin-edit-delete-game.spec.ts`
   - `admin-edit-delete-submission.spec.ts`
   - `highscore-and-personal.spec.ts`
   - `duplicate-submission.spec.ts`
   - `pagination-and-dates.spec.ts`
   - `auth-ui-flows.spec.ts`
   - `invalid-file-upload.spec.ts`

4. Implement 2–3 example tests (image upload and submit-with-screenshot and duplicate submission) to validate the approach.

If you want, I can now scaffold the fixtures folder and the Playwright test skeletons (UI-only) and implement the first two example tests. Tell me which examples to implement first.

