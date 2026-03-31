---
title: "Analyze Frontend — Deduplicate UI & Unify Styles"
description: "Use when: analyze and refactor the frontend to deduplicate UI logic, simplify and unify presentation, extract reusable UI elements and hooks while keeping behavior exactly the same. Produce an agent-executable plan emphasizing read-only subagents for exploration and atomic patches for low-risk fixes."
applyTo: "frontend/**"
---

## Goal
Analyze the frontend code and produce a detailed, prioritized plan to deduplicate UI code, simplify logic without changing runtime behavior, and unify styles and component patterns. The prompt must produce an agent-executable plan (findings, proposed changes, patches, tests, and clarifying questions) and emphasize heavy use of subagents for read-only exploration and isolated edits.

## Inputs (parameters)
- `targetFolder` (string, default: `frontend`) — folder to analyze.
- `deduplicateComponents` (bool, default: true) — extract and centralize duplicated UI elements into reusable components.
- `simplifyLogic` (bool, default: true) — refactor complex or duplicated logic into small, well-named hooks or helpers while preserving identical behavior.
- `unifyStyles` (bool, default: true) — centralize tokens, variables, and style patterns (CSS/SCSS/CSS-in-JS) without changing appearance.
- `createPatches` (bool, default: true) — whether to prepare patch edits for low-risk fixes.
- `runBuildAndTests` (bool, default: true) — attempt `npm install`/`npm run build` and tests after edits when feasible.
- `maxChangesPerRun` (int, default: 10) — keep changes small and atomic.

## Constraints & Hard Rules
- Preserve visible and programmatic behavior exactly (UI observable behavior, DOM structure where tests or scripts rely on it, and public props/JS API must remain compatible unless user approves changes).
- Do not alter backend API contracts or server endpoints as part of frontend refactors.
- Accessibility must not regress. Keep ARIA attributes, focus behavior, and keyboard interactions intact.
- Prefer non-destructive refactors: extract wrappers/components and keep original implementation until imports are switched and tests pass.
- Avoid large framework or library migrations (e.g., React→Svelte, CSS framework rewrites) without explicit user approval.
- Follow existing repository conventions (folder layout, naming, TypeScript/JS style). Do not introduce stylistic churn.
- Add tests for moved/simplified logic (unit tests for hooks/utilities; component tests for extracted components). For visual regressions consider Playwright screenshot tests or Storybook snapshots.

## Agent Workflow (step-by-step, for an agent or subagent to execute)
1. Explore (read-only): use a read-only subagent (e.g., `Explore`) to collect contextual data: list pages/routes, top-level components, `src/components`, `src/hooks`, `src/styles`, global CSS/variables, build scripts, test runners, Storybook, and any visual regression tooling. Produce a one-paragraph architecture summary and a files index (grouped by pages, components, hooks, styles, tests).

2. Surface quick wins: identify files where the same UI element or logic is implemented multiple times, where components contain duplicated markup/styles, where hooks duplicate logic, or where inline styles / CSS fragments repeat. Rank findings by severity (Critical, High, Medium, Low).

3. For each finding produce a structured issue entry:
   - `id` — short id
   - `files` — list of affected files (provide file links)
   - `snippet` — 3–10 line code excerpt showing the problem
   - `severity` — Critical/High/Medium/Low
   - `rootCause` — why it's a problem
   - `proposedFix` — concise refactor summary
   - `risk` — low/medium/high and why

4. Decide edits:
   - If `createPatches` is true and the proposed fix is low-risk, create a small atomic patch using `apply_patch` or `create_file` for new shared components/hooks. Update only the files required to switch usages (keep the change count ≤ `maxChangesPerRun`).
   - For medium/high-risk changes (visual or API-affecting), draft the change as a proposal and add clarifying questions; DO NOT apply changes until user confirmation.
   - When extracting components, prefer additive changes: add new component, wire one or two usages, run tests/build, then switch remaining imports in follow-up runs.

5. Implementation conventions for changes:
   - Place new shared components under `frontend/src/components/ui/` (or existing project component folder). Name folders and files consistently: `Button/Button.tsx` + `Button.module.css` or `Button.tsx` + `button.css` depending on repo style.
   - Put reusable hooks in `frontend/src/hooks/` and utilities in `frontend/src/lib/` or `frontend/src/utils/`.
   - Centralize style tokens into `frontend/src/styles/variables.css` or `frontend/src/styles/tokens.ts` and prefer CSS variables or a single source-of-truth for colors, spacing, and typography.
   - Use existing mapping and formatting utilities; do not duplicate mapping logic — extract common helpers.
   - Use named exports and small, single-purpose functions/components. Keep components focused and composable.
   - Use the project's test tooling (Vitest/Jest/RTL) for unit/component tests and Playwright for critical e2e/visual checks if configured.
   - Maintain consistent code formatting using the repo's existing tools (`prettier`, `eslint`). If missing, propose adding them as a separate low-risk change.

6. Validate edits:
   - Run `cd frontend && npm ci && npm run build` (or `npm install` then `npm run build`) when `runBuildAndTests` is true.
   - Run `npm test` or the repo's test command; for UI changes run the Playwright tests if available to detect regressions.
   - If build/tests fail for reasons unrelated to the small refactor, stop and report the failures rather than making broad fixes.

7. Finalize plan:
   - Output a prioritized, ordered Plan that an agent or engineer can follow: change bundles (id, files, patch), tests to add, and the exact next actions.
   - Provide a short changelog-style summary suitable for a PR description.

## Output (machine- and human-readable)
Return an object (or Markdown document) with these sections:
- `Summary` — one-paragraph architecture summary and high-level recommendation.
- `Findings` — array of issue entries (see structured issue entry above).
- `ProposedChanges` — array of changes. For each change include: `id`, `description`, `filesToChange`, `patch` (if created), `tests` (added/updated), and `risk`.
- `PatchesApplied` — list of `apply_patch`/`create_file` calls performed (if any) and their rationale.
- `Questions` — list of clarifying questions to ask the user before proceeding with medium/high-risk changes.
- `NextSteps` — ordered checklist the agent or developer should follow (include commands to run locally).

Include example JSON/Markdown output at the end so the plan is directly machine-parseable by follow-up agents.

## Clarifying Questions (ask if found during analysis)
- Do you accept creation of shared UI components under `frontend/src/components/ui/` and new hooks under `frontend/src/hooks/`? (yes/no)
- For refactors affecting component props or public module exports, may I change signatures or must I preserve them exactly?
- Should I add unit tests only, or also add visual regression tests (Playwright snapshots) for extracted components?
- Are you open to adding or enforcing a single code-style toolchain (ESLint + Prettier) if missing? (default: propose, do not auto-add)
- Are there specific browsers, screen sizes, or accessibility requirements that must be preserved exactly?

If multiple answers are needed, present them as a short numbered list for the user to reply to.

## Heuristics and Clean-Code Rules (frontend-specific)
- Single Responsibility: each component, hook, or util handles one concern.
- DRY: extract duplicate JSX, styles, and logic into a shared component or hook.
- Small Components: prefer many small, composable components over a single large component.
- Keep render logic pure: avoid side-effects inside render bodies; use hooks for effects.
- Avoid prop drilling: use composition or context for shared state rather than deep prop chains.
- Accessibility-first: preserve keyboard/focus behavior and ARIA attributes when refactoring.
- Visual parity: ensure visual snapshots/unit tests confirm unchanged appearance.

## Subagent & Tooling Guidance (strong emphasis)
- Use a read-only subagent (`Explore`) to gather files and produce the architecture summary. Do not infer structure without reading files.
- Use specialized subagents for edits or `apply_patch` / `create_file` for generating diffs. Keep edits atomic and limited to `maxChangesPerRun`.
- Prefer the following sequence when changing the codebase:
  1. `Explore` to list duplicates and candidates for extraction.
  2. Create a structured issues list (Findings).
  3. For low-risk items, prepare patches that add new components/hooks and update 1–2 usages.
  4. Run build/tests.
  5. If green, expand the patch to switch remaining usages in subsequent runs.
- Use `manage_todo_list` to track steps and `task_complete` when finished.
- Use `run_in_terminal` or `run_task` to run `npm ci`, `npm run build`, `npm test`, and optionally Playwright tasks.

## Example Invocations
- Quick analysis (no edits):
  - `targetFolder=frontend, createPatches=false`
- Apply low-risk fixes automatically:
  - `targetFolder=frontend, createPatches=true, maxChangesPerRun=5`

## Acceptance Criteria
- Duplicate UI is extracted into shared components or hooks where appropriate.
- Behavior (visual and functional) remains unchanged.
- Centralized style tokens exist for colors/spacing/typography where duplication was removed.
- Build succeeds and affected tests pass (when `runBuildAndTests=true`).
- A clear, ordered Plan is produced with patches or questions for the user.

## Iteration & Repeatability
This prompt is parameterized and repeatable. For ongoing cleanup runs, adjust `maxChangesPerRun` and re-run. Keep changes incremental and review each medium/high-risk suggestion with the user.

## If You Encounter Ambiguity
Stop and present the minimal set of clarifying questions. Avoid making guesses that change public behavior, visual appearance, or test expectations without explicit approval.

---

*Generated following the agent-customization SKILL guidelines. Use read-only subagents for exploration and `apply_patch`/`create_file` for edits.*
