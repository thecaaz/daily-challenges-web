---
name: csharp-backend
description: "**WORKFLOW SKILL** — C# ASP.NET Core backend development workflow: design APIs, implement controllers, DTOs, services, repositories, EF Core migrations, tests, logging, error handling, security, performance, and release checks."
applyTo:
  - path: "backend/**"
  - path: "src/**"
  - language: "csharp"
tags:
  - csharp
  - aspnetcore
  - efcore
  - backend
  - dotnet
  - testing
  - ci
---

# C# Backend Development Skill

**Overview**
A reusable workflow for implementing, reviewing, and shipping C# ASP.NET Core backend features. Covers API design, DTOs/mapping, controllers, services/repositories, EF Core migrations, dependency injection, testing (unit/integration), logging, error handling, security checks, and release readiness.

**When to use**
- Adding or changing API endpoints and domain behavior.
- Implementing or refactoring data access (EF Core / repositories).
- Adding migrations, seeding, or schema changes.
- Writing unit/integration tests and e2e hooks for backend logic.
- Reviewing backend code for security, performance, and maintainability.

## Step-by-step Workflow
1. Define intent & acceptance
   - Ask: desired endpoint, inputs/outputs, auth, idempotency, performance.
   - Produce a small API contract (route, verb, DTOs, sample JSON responses).
2. Data model & persistence
   - Determine schema changes required; pick EF Core model changes or migration approach.
   - Decide if repository pattern, CQRS, or direct DbContext use is appropriate.
3. DTOs & mapping
   - Create DTOs for inputs/outputs; keep domain models separate.
   - Add mapping via `AutoMapper` or explicit helper methods.
4. Implement service layer
   - Put business logic in services with clear dependency injection and single responsibility.
5. Controller & routing
   - Add controller actions that call services and return appropriate status codes.
6. Validation & error handling
   - Validate inputs (data annotations / FluentValidation). Map exceptions to HTTP responses.
7. Persistence changes & migrations
   - Add migration(s) and verify DB update works locally. Provide rollback guidance when necessary.
8. Tests
   - Unit tests for services and mapping.
   - Integration tests for controllers (in-memory or test DB).
9. Logging, metrics, and security checks
   - Add structured logging, validate sensitive-data handling, and enforce auth/authorization.
10. Performance & edge cases
   - Check N+1 queries, use async/await, add indexes if needed.
11. Release checklist
   - `dotnet build`, `dotnet test`, migrations verified, linting/style checks, CI passes, runtime config reviewed.

## Decision Points
- Use EF Core migrations when schema evolves; use feature flags for data migrations that must be staged.
- Prefer repository + unit-of-work for complex domain logic; direct `DbContext` for simple CRUD.
- Choose DTO shape by separating public API needs from domain model (avoid exposing internal fields).
- Async endpoints for I/O-bound operations; sync allowed only for trivial fast operations.

## Quality Criteria / Completion Checks
- Code compiles: `dotnet build backend` succeeds.
- Tests: `dotnet test` (or `dotnet test backend`) pass locally.
- Migrations: created and applied locally without data loss in dev environment.
- Security: no direct SQL injection paths, auth enforced, secrets not logged.
- Performance: no obvious N+1 queries; endpoints respond within acceptable latency.
- CI: pipeline job(s) pass, and any new pipeline steps (migrations, DB seeding) documented.

**Quick commands**
- Build: `dotnet build backend`
- Test: `dotnet test backend`
- Add migration (example): `dotnet ef migrations add AddX -p backend -s backend`

## Example Prompts (use with this skill)
- "Add POST /api/games to create a Game with title, url, and optional image. Include DTOs, validation, service method, EF Core migration, and unit tests."
- "Refactor GamesRepository to use async EF Core queries and fix test failures; include integration test updates."
- "Review `GamesController` for auth/authorization and suggest fixes for potential security issues."
- "Create an EF Core migration that adds `IsFeatured` to Games and update seeding logic." 

## Templates & Frontmatter Guidance
- Recommended location: `.github/skills/csharp-backend/SKILL.md` for workspace-shared skills.
- Keep `description` discoverable: include trigger phrases like "API", "EF Core", "migration", "controller" so the agent can match intent.
- Use `applyTo` globs narrowly (e.g., `backend/**`) to avoid always-loading the skill in unrelated contexts.

## Clarifying Questions
- Scope: workspace-shared skill (default) or personal (`{{VSCODE_USER_PROMPTS_FOLDER}}`)?
- Target framework/version: .NET 7, .NET 8, or other?
- Test strategy: use real test DB, in-memory, or dockerized test database in CI?

## What this skill produces
- A reproducible checklist and templated prompts to implement, review, and ship C# backend changes with high confidence.

## Suggested Next Customizations
- Add prompt templates for "create controller + service + tests" and "create migration + seed".
- Create mapping helper prompts for common DTO patterns.
- Add an instructions file (`*.instructions.md`) that auto-injects code style rules and common nuget choices.