# Performance & Scalability Ideas

Notes: Improvements to support scale and responsiveness.

- Redis Caching for Leaderboards
  - Description: Cache computed leaderboards and hot pages to reduce DB load and latency.
  - Impact: High. Effort: Medium.

- Async Workers for Image Processing
  - Description: Offload image resizing, thumbnail generation, and OCR to background workers.
  - Impact: High. Effort: Small–Medium.

- CDN for Static Assets
  - Description: Serve frontend assets and uploaded images via a CDN for lower latency.
  - Impact: High. Effort: Small.

Suggested next steps:
- Identify hot endpoints and add caching for leaderboard queries.
- Add background worker and queue for image tasks.
