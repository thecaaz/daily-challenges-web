# Ideas

## Browser Extension

- **MV3 migration** — Required for Chrome Web Store publication. Currently Manifest V2 will be sunset.
- **More game adapters** — Wordle, Connections, Waffle, and other high-traffic daily games would expand the user base considerably. Each adapter is relatively small to write.
- **Auto-submit** — For confident score reads, skip opening the submission form entirely and submit directly. Opt-in per user.
- **Adapter confidence indicator** — Show how confident the extension is in its score read before the user submits, to reduce accidental wrong-score submissions.

## Social & Community

- **Leagues / groups** — Small invite-only groups with their own leaderboard. This is the primary way friend groups actually want to compete — against each other, not the whole platform.
- **Quick reactions on leaderboard entries** — Emoji reactions (👏, 🔥, 😬) on a daily result. Low effort to build, high social stickiness.
- **Weekly digest notification** — A summary of the week: your best game, your rank changes, who's been dominant. Could be in-app only, no email needed.
- **"Beaten by a friend" notification** — When a friend submits a better score than yours on the same scoring day, you get a nudge. Drives re-engagement and friendly rivalry.

## Gamification

- **Weekly leaderboards** — Cumulative XP or wins over the past 7 days. Gives players who missed the daily another way to be competitive. Much lower commitment than all-time rankings.
- **Personal records per game** — Clearly surface a user's all-time best score for each game on their profile. Simple but motivating.
- **Comeback XP** — Small XP bonus when a user returns after a streak break. Reduces the "I already lost my streak, no point playing" drop-off.

## UX & Mobile

- **PWA / installable app** — Add a web app manifest and service worker so mobile users can install it to their home screen. The daily-game audience skews heavily mobile.
- **Score input improvements** — Smarter score field: auto-detect format hints from the game config, show an example input, validate before submission rather than after.

## Games & Content

- **Game tags / categories** — Tag games as "word", "geography", "math", "visual" etc. Lets users filter and discover games they haven't tried. Especially useful when the game list grows.
- **Game descriptions and tips** — Editable per-game markdown field for rules, tips, or scoring explanation. Useful for newcomers who don't know what "3/6" means for a given game.
- **Featured / pinned games** — Admin can pin 1–3 games to the top of the list (e.g., this week's featured challenge). Drives participation on specific games.

## Admin & Operations

- **Submission trends dashboard** — Simple charts: daily active users, submissions per game over time, streak retention. Helps admins understand what's working.
- **Duplicate / suspicious submission detection** — Flag submissions where the same user submits an identical score+screenshot within a short window. Not automated rejection, just a review queue.
- **Game import / bulk add** — A small form to add multiple games at once by URL, useful when onboarding a new community with a pre-existing game rotation.
