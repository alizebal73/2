# Stage 12 — Checkpoint 2026-10-03

## Active branch

`stage12-session-lease`

## Stable main

`main` = `93ba89c9231bd821f45f93ab2aa7b9c25b8cc338`

## Stage 12 baseline

`7d6d6f753ab7741b6ea29a57a3a2beb1b34b348b`

Stage 12 must not modify `main` directly.

## Current branch head

`9e1b8f7219e39991b508f09db4c74a307eddc811`

## Current CI

Run #1007+ on the evolving Stage 12 head; previous runs were cancelled by the branch concurrency guard while the branch was being corrected. The latest head has not yet produced a completed verdict.

Current pre-CI status:
- Agent command transport: implemented with stable per-device SignalR group.
- Stage 12 Agent Session → Game → Lease → Credential → EndSession/Release smoke: added.
- Game activeUsers: authoritative from active Sessions.
- Stale/Disconnect Lease recovery: implemented.
- Customer authentication smoke restored before the Stage 12 flow.

## What is actually implemented

- Session can reference Game and AgentDevice.
- AccountPoolEntry keeps encrypted credential material separately from SecretHash.
- AccountLease has a short credential-access expiry.
- Operational Session allocation validates Session + Game + Agent + CustomerLogin.
- Allocation is atomic and creates a Session-bound Lease.
- Agent credential retrieval is Lease-token + Agent-bound and denied outside the active window.
- Session end releases the Lease.
- Agent disconnect/stale heartbeat releases active Leases.
- Game active-user count is derived from authoritative active Sessions.
- Legacy GameAccount remains separate.
- Dashboard never receives raw account secrets.
- Stage 12 has an Agent/Session/Lease credential E2E smoke path.

## Current blocker

The current branch head is awaiting a completed CI run. Do not mark Stage 12 Done until the exact head is green for Build, Tests, EF validation, Server Smoke, and the required Agent/Session/Lease E2E.

The previous Agent ping timeout was treated as a transport race and corrected without weakening the timeout assertion.

## Important recovery rule

If work is interrupted, resume from this file first. Do not infer progress from chat memory.

## Working method

One logical change per checkpoint:
- inspect
- implement
- test
- commit
- record SHA/state
- continue

For long work, do not keep one giant uncommitted sequence. Every major slice must leave a recoverable commit and an updated checkpoint.

## Do not do

- Do not edit `main` for Stage 12.
- Do not weaken CI assertions just to get green.
- Do not recreate fake Apply/Sync success.
- Do not replace legacy `GameAccount`.
- Do not call Stage 12 Done until the same branch SHA has green Build, Tests, EF validation, Server Smoke and required Agent/Session/Lease E2E.
