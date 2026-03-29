---
name: plan-feature
description: "Create a scoped, project-aware implementation plan for a named feature. Uses subagents to explore the repo, produce file-level change lists, DB/migration notes, API contracts, task estimates, tests, rollout and monitoring guidance. Ask clarifying questions when inputs are incomplete."
applyTo:
  - "backend/**"
  - "frontend/**"
tags:
  - planning
  - feature
  - subagent
---

## Usage
Provide the feature you want planned and optional context. The prompt accepts a single JSON or YAML object with these fields:
- `feature_name` (required): short descriptive name
- `summary` (optional): 1-2 sentence description of intent and user value
- `scope` (optional): `backend`, `frontend`, `both`, `infra`, or `docs`
- `priority` (optional): `low`, `medium`, `high`
- `constraints` (optional): technical or business constraints (storage, backwards-compatibility, auth, sizing)
- `known_files` (optional): list of workspace-relative file paths to prioritize
- `stakeholders` (optional): owners or reviewers to involve

Example invocation (JSON):

```
{
  "feature_name": "User avatar uploads",
  "summary": "Allow users to upload a profile image in Settings; show avatar in posts and profiles.",
  "scope": "both",
  "priority": "medium",
  "constraints": "use existing blob storage service; max 5MB images; preserve backwards-compatibility for API v1",
  "known_files": ["backend/Models/User.cs", "backend/Controllers/AuthController.cs"]
}
```

## Prompt for the agent (what to do)
1. Immediately validate the input. If any required or clarifying information is missing (scope, priority, constraints, or acceptance criteria), ask the user a concise clarifying question before performing repo exploration.

2. When inputs are sufficient, plan the work with the entire repository in mind by using subagents to gather focused context. Recommended subagent flow:
   - Run the `Explore` subagent (quick) to get a high-level map of likely relevant folders and files (controllers, services, DTOs, models, migrations, frontend pages/components).
   - Run `search_subagent` or a similar code-search subagent to find files that mention the primary domain terms (feature name, model names, controller names, DTOs). Use regex/keyword alternation (e.g., `User|Avatar|Profile`) to broaden matches.
   - For each candidate file, read the file(s) and extract the minimal facts needed (class names, public methods, DTO shapes, existing endpoints, DB columns). Keep each file summary to 1-3 lines and include workspace-relative links.

3. Synthesize a project-aware implementation plan containing these sections (use clear headers):
   - **Summary:** 1-3 lines describing the feature and the intended user value.
   - **Goals & Success Metrics:** measurable targets (e.g., upload success rate, storage size limits, latency targets).
   - **Acceptance Criteria (Testable):** explicit items that define done.
   - **Scope In / Out:** what is included and what is out of scope.
   - **API Contract (if applicable):** endpoints, HTTP methods, request/response DTOs, authentication/authorization rules, example payloads.
   - **Database Changes:** tables/columns to add/change, migration steps, data migration strategy, backward compatibility concerns.
   - **Backend Implementation Notes:** services/repositories/controllers to change or add (list workspace-relative file links), important helper functions or libraries to reuse.
   - **Frontend Implementation Notes:** pages/components to modify or create, UX copy, validation rules, upload preview behavior, file size limits.
   - **Tests:** unit tests, integration tests, and e2e tests to add or update; test-data needs and test-file suggestions.
   - **Deployment & Rollout:** feature flags, phased rollout, migration window, rollback plan.
   - **Monitoring & Alerts:** metrics to emit, dashboards to add, alert thresholds.
   - **Estimation & Tasks:** break into ticket-sized tasks with size/effort (Small/Med/Large + hours or story points).
   - **Files Likely To Change:** concise list of workspace-relative files (links) the plan expects to edit.
   - **PR Description Template:** suggested PR title, description, testing notes, migration notes, and reviewers.
   - **Risks & Mitigations:** top 3 risks and quick mitigations.
   - **Open Questions:** only the explicit clarifying questions you need the user to answer — do not encode uncertain assumptions as tasks.

4. Formatting and links
   - Present the plan as structured Markdown with clear headers and bullet lists.
   - Where you reference files, use workspace-relative links like `[backend/Controllers/AuthController.cs](backend/Controllers/AuthController.cs#L1)` to help the user jump to the file.
   - Keep the exploration evidence concise; place detailed subagent outputs in an appendix if needed.

5. Follow-up actions
   - After the user approves the plan, offer to generate a minimal patch (diff) or a PR checklist for the first task.
   - If asked, produce code sketches for critical pieces (DTOs, migration skeleton, controller endpoints) but do not commit code without explicit permission.

## Subagent usage guidance (recommended)
- Use the `Explore` subagent first (quick) to map high-level candidates.
- Use `search_subagent` to target files by keywords, then read the top matches with `read_file`.
- Use multiple short subagent runs rather than one long run; keep each run focused (controllers → models → migrations → frontend).
- Keep subagent outputs narrow and actionable; summarize and link them into the final plan.

## Safety and correctness rules
- Never add uncertain implementation steps directly to the final "Tasks" section — instead, list them under "Open Questions" and ask the user.
- When recommending DB migrations, always include a rollback/migration-backwards strategy.
- If a suggested change touches authentication, permissions, or user data, call it out as high-risk and require explicit approval.

## Outputs (deliverables)
Return exactly one Markdown document containing the plan with the sections above. Also include a short 3-line executive summary at the top and a concise list of 3–8 next steps.

## Example (very brief output snippet)

**Summary**
- Add profile image upload to user settings to improve personalization.

**Acceptance Criteria**
- User can upload a PNG/JPG ≤ 5MB and see the avatar on their profile within 3s.
- Uploads stored in existing blob storage and accessible from public profile pages.

**Files Likely To Change**
- [backend/Models/User.cs](backend/Models/User.cs#L1)
- [backend/Controllers/AuthController.cs](backend/Controllers/AuthController.cs#L1)
- [frontend/src/pages/Settings.jsx](frontend/src/pages/Settings.jsx#L1)

**Next steps**
1. Confirm image size limit and storage choice.
2. Run quick repo exploration for `User` and `Profile` symbols.
3. Produce the full plan and task breakdown.

---

If you want, I can now run the recommended subagent exploration (Explore quick + targeted search) and draft the first full plan for your specified feature. Provide the invocation object above to start.