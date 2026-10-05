# Stage 14 — Checkpoint 2026-10-05

## Base

main = c6bbcd39848fcff1825cec21ef3083b6b3d3435b

## Slice 14.1 — Settings Information Architecture

### Scope

- Settings page remains behavior-compatible with the previous localStorage-backed implementation.
- Category navigation added for Dashboard/Display, Sessions/Settlement, Alerts, Network, Backup/Recovery, Security/Access, Users/Shift, Games/Client, Appearance/Localization, and Hotkeys.
- Settings search filters the category navigation by user-facing labels and keywords.
- Category buttons scroll to stable settings anchors.
- The page explicitly communicates that current settings are local-only; no fake Server-backed Settings capability was introduced.
- Browser Smoke covers settings search and category navigation.

### Not Done

- Exact-head CI certification for 14.1.
- Server-backed Settings.
- Dashboard Internet 1/2 visual grouping and dense responsive hardening.
- Remaining accessibility/operational UI polish.

## Recovery Rule

Resume from branch `stage14-settings-ia` and the exact HEAD reported by CI. Do not infer completion from chat history.

## Next

After 14.1 Green, merge to main and start **Stage 14.2 — Dashboard PC grouping by Internet 1/2 + dense responsive hardening**.
