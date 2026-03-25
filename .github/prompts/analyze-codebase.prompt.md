---
name: Analyze Codebase for Smells and Duplications
description: "Analyze the repository to find code smells, unused code, duplicated logic, and produce a prioritized remediation plan. Use repository context and produce file-linked findings and an actionable fix plan."
scope: workspace
applyTo: "**/*"
---

When invoked, perform a structured, repeatable analysis of the workspace codebase and return a prioritized remediation plan.

Inputs (optional):
- `scope`: path or glob to limit analysis (default: repository root)
- `depth`: `quick` | `medium` | `thorough` (default: `medium`)
- `languages`: list of languages to focus on (e.g., `C#`, `JS`, `TS`)
- `producePatches`: `true` | `false` — include suggested minimal patches for low-risk fixes (default: `false`)

Required analysis steps:
1. Inventory
   - Enumerate source files, tests, build artifacts, and third-party code in `scope`.
   - Report languages and approximate LOC per language.
2. Smell detection
   - Find obvious code smells: very large files/classes, very long functions, very high cyclomatic complexity, tightly coupled modules, multiple responsibilities, duplicated logic, inconsistent naming, magic numbers, commented-out code, and missing/null handling.
3. Unused code detection
   - Identify files, functions, classes, or exports that appear unused (no references, not imported, not exercised by tests, or only present as dead build artifacts).
4. Duplication detection
   - Detect duplicated logic across files using token/AST similarity and surface-level string matches.
5. Risk & severity
   - For each finding, assign severity: `critical`, `high`, `medium`, `low` and explain reasoning.
6. Remediation plan
   - Produce a prioritized list of fixes grouped by risk and effort. For each fix include:
     - Short description
     - Files and line ranges (provide repo-relative links)
     - Suggested change (concise diff or pseudocode)
     - Estimated effort (S/M/L) and any test impacts
     - Whether the change is safe to do automatically (yes/no)
7. Optional patches
   - If `producePatches:true`, generate minimal patches for low-risk fixes (format: unified diffs) and mark them as ready-to-apply.
8. Output format
   - Top-level summary (counts, hotspots)
   - Findings table (file link, lines, issue type, severity)
   - Detailed sections with code snippets (with line ranges) and suggested fixes
   - Final prioritized plan with steps and a suggested PR checklist

Behavioral rules and constraints:
- Use repository context; prefer static analysis and reference checks over heuristics when possible.
- When uncertain about whether code is unused, list the evidence and mark as `needs-confirmation` rather than deleting.
- For duplicated code, prefer recommending extraction/refactor rather than blind deletion.
- Keep suggestions minimal and clearly label any change that could break behavior.
- When presenting file locations, use repo-relative paths and include line-range links where possible.

When ambiguous, ask the user one concise question clarifying the `scope`, `depth`, or whether to produce patches.

Example invocations:
- Quick scan across repo root: `/analyze --depth quick`
- Thorough C#-only: `/analyze --scope backend --languages C# --depth thorough --producePatches false`
- Generate low-risk patches for JS: `/analyze --scope frontend/src --languages JS,TS --producePatches true`

Notes: This prompt is workspace-scoped and intended for recurring use by developers to create actionable PRs or tasks from findings.
