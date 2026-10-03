# Stage 12 — Checkpoint 2026-10-03

## Active branch

`stage12-session-lease`

## Stable main

`main` = `93ba89c9231bd821f45f93ab2aa7b9c25b8cc338`

## Stage 12 baseline

`7d6d6f753ab7741b6ea29a57a3a2beb1b34b348b`

Stage 12 must not modify `main` directly.

## Current branch head

`48f07290e4c5c2b587955136656f6d7b192d4d86`

## Current CI

Run #976 on the exact Stage 12 head:
- Build .NET: ✅
- Test .NET: ✅
- EF snapshot validation: ❌
- Server/Dashboard steps were skipped because snapshot validation failed.

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

EF reports pending model changes.

This is NOT evidence that the Stage 12 business flow is wrong. The failure is in the committed EF model snapshot. The current snapshot was manually edited across earlier stages and is not byte/structure-equivalent to the model EF scaffolds today.

## Correct next action

1. Regenerate the complete `GameNetDbContextModelSnapshot.cs` from the actual current EF model on this branch.
2. Verify that the Stage 12 migration contains only database operations that are genuinely not already represented by earlier migrations.
3. Run:
   Build → Test → EF snapshot validation → migration/startup smoke.
4. Only after that continue Stage 12 business hardening.
5. Do not merge PR #18 until the exact branch head is green.

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
