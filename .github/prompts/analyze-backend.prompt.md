---
title: "Analyze Backend — Enforce Service-Class Logic"
description: "Use when: analyze and refactor the repository backend to ensure business logic lives in service classes, follow clean-code principles, produce a repeatable, agent-executable plan, and prefer subagents for read/edit tasks."
applyTo: "backend/**"
---

## Goal
Analyze the backend code and produce a detailed, prioritized plan to move and/or implement business logic inside service classes, improve code clarity, and propose safe, test-backed edits. The prompt must produce an agent-executable plan (findings, proposed changes, patches, tests, and clarifying questions) and emphasize heavy use of subagents for reading and editing.

## Inputs (parameters)
- `targetFolder` (string, default: `backend`) — folder to analyze.
- `enforceServiceClasses` (bool, default: true) — require business logic to be in `Services/` classes.
- `createPatches` (bool, default: true) — whether to prepare patch edits for low-risk fixes.
- `runBuildAndTests` (bool, default: true) — attempt `dotnet build` and unit tests after edits when feasible.
- `maxChangesPerRun` (int, default: 10) — keep changes small and atomic.

## Constraints & Hard Rules
- All business/domain logic must reside in service classes under `Services/` (create `I{Name}Service` + `NameService` where appropriate).
- Controllers may only: accept requests, validate/parse input, call services, and return responses (mapping + HTTP concerns only).
- Data access must remain in repository/DbContext layers (do not push DB logic into controllers or services that bypass repositories without user approval).
- Follow existing repository naming conventions, folder layout, and coding style. Avoid stylistic churn.
- Comments should be minimal: prefer self-explanatory code. Add comments only when the algorithm is complex and the comment provides meaningful insight.
- Do not perform unsafe schema changes or destructive database migrations without explicit user approval.
- If a fix is not immediately obvious or carries risk, STOP and ask the user clarifying questions (see Questions section below).

## Agent Workflow (step-by-step, for an agent or subagent to execute)
1. Explore (read-only): use a read-only subagent (e.g., `Explore`) to collect contextual data: list controllers, services, repositories, models, DTOs, mapping utilities, DbContext, and tests. Produce a one-paragraph architecture summary and a files index.

2. Surface quick wins: identify files where controllers contain heavy business logic, duplicated code, or long methods. Rank findings by severity (Critical, High, Medium, Low).

3. For each finding produce a structured issue entry:
   - `id` — short id
   - `files` — list of affected files (provide file links)
   - `snippet` — 3–10 line code excerpt showing the problem
   - `severity` — Critical/High/Medium/Low
   - `rootCause` — why it's a problem
   - `proposedFix` — concise refactor summary
   - `risk` — low/medium/high and why

4. Decide edits:
   - If `createPatches` is true and the proposed fix is low-risk, create a small atomic patch using the workspace edit tool (apply_patch). Include unit tests or modify existing tests where appropriate.
   - For medium/high-risk changes, draft the change as a proposal and add clarifying questions; do NOT apply changes until user confirmation.

5. Implementation conventions for changes:
   - New services go under `backend/Services/` or existing Services namespace. Create interfaces `I{Name}Service` and implementation `NameService`.
   - Methods should be single-purpose and small; extract helpers for readability.
   - Use constructor injection for dependencies (DbContext, repositories, loggers).
   - Move mapping logic to `Mapping/DtoMapper` where present; do not duplicate mapping.
   - Add unit tests focused on moved logic; prefer small, fast unit tests mocking external dependencies.

6. Validate edits:
   - Run `dotnet build backend` (or `dotnet test` for affected test projects) when `runBuildAndTests` is true.
   - If build/tests fail due to unrelated flakiness, report rather than attempt broad fixes.

7. Finalize plan:
   - Output a prioritized, ordered Plan that an agent or engineer can follow: change bundles (id, files, patch), tests to add, and the exact next actions.
   - Provide a short changelog-style summary suitable for a PR description.

## Output (machine- and human-readable)
Return an object (or Markdown document) with these sections:
- `Summary` — one-paragraph architecture summary and high-level recommendation.
- `Findings` — array of issue entries (see structured issue entry above).
- `ProposedChanges` — array of changes. For each change include: `id`, `description`, `filesToChange`, `patch` (if created), `tests` (added/updated), and `risk`.
- `PatchesApplied` — list of apply_patch calls performed (if any) and their rationale.
- `Questions` — list of clarifying questions to ask the user before proceeding with medium/high-risk changes.
- `NextSteps` — ordered checklist the agent or developer should follow (include commands to run locally).

Include example JSON/Markdown output at the end so the plan is directly machine-parseable by follow-up agents.

## Clarifying Questions (ask if found during analysis)
- Do you accept creation of service interfaces (`I{Name}Service`) and move implementation files into `backend/Services/`? (yes/no)
- For refactors affecting public controller endpoints, may I change method signatures or must I preserve API contracts exactly?
- Should I add unit tests only, or also integration tests for moved logic?
- Are you okay with small automatic migrations if a refactor requires a DB schema change? (default: no)
- Preferred test runner/CI commands to validate changes locally?

If multiple answers are needed, present them as a short numbered list for the user to reply to.

## Heuristics and Clean-Code Rules (enforced by the prompt)
- Single Responsibility: each service handles one business concern.
- Names: use descriptive method and class names that explain intent.
- Keep methods short (prefer < 30 lines); extract helpers for readability.
- Avoid deep nesting; return early.
- Prefer composition over inheritance unless there's a clear domain need.
- Favor explicit over implicit behavior; do not rely on magic strings.

## Subagent & Tooling Guidance (strong emphasis)
- Use a read-only subagent (e.g., `Explore`) to gather files and context first. Do not infer structure without reading files.
- Use specialized subagents for edits or `apply_patch` for generating diffs. Keep edits atomic and limited to `maxChangesPerRun`.
- Use `run_in_terminal` or `run_task` to run `dotnet build` and `dotnet test` when validating changes.
- Use `manage_todo_list` to track progress and `task_complete` when finished.

## Example Invocations
- Quick analysis (no edits):
  - targetFolder=backend, createPatches=false
- Apply low-risk fixes automatically:
  - targetFolder=backend, createPatches=true, maxChangesPerRun=5

## Acceptance Criteria
- Controllers are thin: no business logic remains (only orchestration, validation, mapping).
- All moved/created logic lives in `Services/` and has unit tests covering behavior.
- Build succeeds and affected tests pass (when `runBuildAndTests=true`).
- A clear, ordered Plan is produced with patches or questions for the user.

## Iteration & Repeatability
This prompt is parameterized and repeatable. For ongoing cleanup runs, adjust `maxChangesPerRun` and re-run. Keep changes incremental and review each medium/high-risk suggestion with the user.

## If You Encounter Ambiguity
Stop and present the minimal set of clarifying questions. Avoid making guesses that change public behavior or the database schema without explicit approval.

---

*Generated following the agent-customization SKILL guidelines. Use read-only subagents for exploration and apply_patch (or create_file) for edits.*
