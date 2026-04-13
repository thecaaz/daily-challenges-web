---
name: efcore-migrations
description: "**WORKFLOW SKILL** — EF Core migrations workflow for backend model changes: generate, review, and apply EF Core migrations using the repository's README guidance. Agents MUST NOT hand-edit migration code — migrations must be generated with the `dotnet ef migrations add` tool."
applyTo:
  - path: "backend/**"
  - language: "csharp"
tags:
  - efcore
  - migration
  - database
  - backend
  - dotnet
---

# EF Core Migrations Skill

**Overview**
A compact, reproducible workflow for creating and applying EF Core migrations after entity/model changes. This skill codifies repo-specific `dotnet ef` commands (from the repo README) and enforces the rule: do not hand-generate migration code — always use the EF tooling to produce migration files.

**When to use**
- After model/entity (domain) changes in `backend/Models` or other persisted types.
- When adding, renaming, or removing columns or relationships that require schema changes.
- When preparing data migrations or seed updates tied to a schema change.

**Prerequisites**
- Project builds: run `dotnet build backend` and fix compile errors first.
- `dotnet-ef` tooling available (global or local). If not available, prompt the user before attempting to install or run it.
- Local dev DB or test DB available when you plan to run `dotnet ef database update`.

**Repository reference**
Follow the repository README's "Migrations" section for the canonical commands and locations: [README.md](README.md). Example commands from the README:

```
# create migration
dotnet ef migrations add <Name> --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj

# apply migration
dotnet ef database update --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj
```

Always review generated migration files under `backend/Migrations` before committing or applying them.

## Step-by-step Workflow (agent)
1. Confirm intent
   - Detect model/entity changes, or confirm the user-declared changes.
   - Ask: migration name, target environment (dev/test/staging), and whether to run the DB update now.
2. Build & tests
   - Run `dotnet build backend` and `dotnet test` (or ask permission before running tests).
3. Generate migration (tooling-only)
   - Run the `dotnet ef migrations add <DescriptiveName>` command using the repository flags from the README (project/startup-project).
   - Do NOT hand-author migration files. If tooling cannot run, stop and ask the user for permission to install/run `dotnet-ef` or for instructions to proceed locally.
4. Review generated migration files
   - Inspect `backend/Migrations/<timestamp>_<Name>.cs` and `.Designer.cs` for expected schema changes and any surprising operations (drops, data-loss operations).
   - If the migration includes risky operations (column drops, data-moving SQL), ask for a migration plan and confirm backups/test-run strategy.
5. Apply to local/test DB (optional)
   - If requested and safe, run `dotnet ef database update` against the dev/test DB and verify runtime behavior.
6. Commit & document
   - Add generated migration files to VCS, include a short commit message describing the model change and migration name.
   - Update any seed data or documentation if needed.
7. CI/Release considerations
   - Ensure CI builds and tests run migrations as part of schema/setup steps or document required manual steps for deploy.

## Decision Points
- Rolling schema changes vs. destructive changes: if data-loss operations are detected, require an explicit manual approval and a rollback plan.
- Feature-flagged data migrations: prefer background/controlled data migrations when converting large datasets.
- Tooling missing: do not generate placeholder migration files; ask the user to run the tooling locally or permit the agent to install/run `dotnet-ef`.

## Quality Criteria / Completion Checks
- `dotnet build backend` succeeds.
- Migration files are created under `backend/Migrations` and reviewed.
- `dotnet ef database update` (when run) applies without unexpected errors in the dev/test DB.
- Unit and integration tests that depend on the schema pass locally/CI.
- Commit contains only the generated migration files and related code (no handcrafted migration content).

## Quick commands (repo-specific)
- Create migration:

  dotnet ef migrations add <Name> --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj

- Apply migration:

  dotnet ef database update --project backend/DailyChallenges.csproj --startup-project backend/DailyChallenges.csproj

- Build: `dotnet build backend`
- Test: `dotnet test backend`

## Example prompts (use with this skill)
- "I've added `IsFeatured` to `Game`. Create an EF Core migration named `AddIsFeaturedToGames` using the README commands, run it against my dev DB, and commit the generated migration files."
- "Model updated: renamed `Score` -> `Points` on `Submission`. Generate the migration but do NOT apply it—I'll review before apply."
- "I can't run dotnet-ef here; show exact commands and steps I should run locally to create and apply the migration."

## Clarifying Questions (ask before acting)
- May I run `dotnet ef` in this workspace? (requires SDK and dotnet-ef)
- Which environment should I target when running `database update`? (`dev`/`tests`/`staging`/`none`)
- Is this change allowed to be destructive (drops/renames that may lose data)? If so, provide the rollback plan.

## What this skill produces
- A validated migration workflow and the generated migration files created by `dotnet ef migrations add` (never hand-crafted migration code).
- Clear review guidance and VCS commit instructions.

## Suggested next customizations
- Add a prompt template `create-migration.prompt.md` that runs the `dotnet ef` command when the user grants permission.
- Add an agent persona note (e.g., in `senior-csharp-backend.agent.md`) to prefer this skill for DB/schema tasks.

---

When you want me to create migrations automatically, confirm I may run `dotnet ef` in the workspace and whether to apply the migration to the dev/test DB immediately. If `dotnet-ef` is unavailable, I will stop and provide exact commands for you to run locally.
