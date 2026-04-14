# Backend Feature Ideas

Notes: Backend ideas focused on data, validation, scale, and integrations.

- Advanced Leaderboards & Time Windows
  - Description: Support daily/weekly/monthly/global leaderboards and arbitrary time-range queries.
  - Impact: High. Effort: Medium.

- Submission Verification Pipeline
  - Description: Background workers to validate uploads (image integrity, duplicate detection, OCR heuristics) and flag suspicious submissions for review.
  - Impact: High (trust & anti-cheat). Effort: Large.

- Team / Clan Support
  - Description: Support team-based submissions and team leaderboards (team pages, invites, shared stats).
  - Impact: Medium. Effort: Large.

- Public API & Rate-limited Keys
  - Description: Read-only API for leaderboards/games with API key issuance and rate limits for third-party integrations.
  - Impact: Medium. Effort: Small.

- Scheduled Daily Challenge Generator
  - Description: Cron/scheduled job to rotate/promote daily challenges and optionally send digest notifications.
  - Impact: Medium. Effort: Small.

Suggested next steps:
- Design API contract for leaderboards and rate limits.
- Prototype a verification worker that runs OCR and basic heuristics.
