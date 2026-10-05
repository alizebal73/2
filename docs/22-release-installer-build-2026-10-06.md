# Release Installer Build — 2026-10-06

This build branch exists only to execute the repository's official Release installer workflow against the current `main` baseline.

Expected artifacts:
- `GameNetManager-Server-Setup-<version>.exe`
- `GameNetManager-Client-Setup-<version>.exe`
- `SHA256SUMS.txt`

Build source:
- Server: self-contained win-x64 Release publish
- Client: self-contained win-x64 Release publish
- Dashboard: production build included in Server publish
- Installer compiler: Inno Setup
- Runner: self-hosted Windows x64

This record does not bypass the separate physical 2–3 PC validation gate.
