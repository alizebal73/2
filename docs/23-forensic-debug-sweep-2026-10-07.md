# Forensic Debug Sweep — 2026-10-07

## Purpose

This checkpoint changes the debugging method from symptom-by-symptom patching to root-cause / regression-driven debugging.

Repository: `alizebal73/2`
Base SHA reviewed: `d8031070413338c50fd67155a8074c45c91b5f56`
Working branch: `debug/forensic-sweep-2026-10-07`

## Rules for this sweep

1. Do not mark a finding fixed from UI appearance alone.
2. Every confirmed defect must have a root cause, a code change, and a regression scenario.
3. Old findings are re-checked against current code before changing them.
4. Main stays untouched until the isolated sweep is validated on the user's self-hosted Windows runner.
5. No broad refactor while the product is unstable.

## Confirmed root causes found in the current code

### RED — Agent registration could rotate an existing DeviceId token

`/api/agent/register` accepted the pairing code and then regenerated the bearer token even when the submitted `DeviceId` already existed.

Impact:
- a pairing-code holder could rotate an existing Agent credential;
- an existing Client could silently lose its previous bearer token;
- one bootstrap credential could become a device-credential replacement mechanism.

Patch on this branch:
- pairing code is now first-time bootstrap only;
- an existing `DeviceId` requires the legacy registration-token path;
- pairing-code usage happens after device/station validation;
- registration receives an IP-based rate limit.

### RED — Customer authentication was not bound to the actual registered Agent

`/api/customer-auth/login` authenticated the username/password and accepted a caller-supplied `clientKey`, but did not require that this key matched the currently resolved registered Agent for the requesting PC.

Impact:
- the customer authentication contract could be detached from the physical Client identity;
- later state checks could reject the same login that had already been created;
- device ownership assumptions could diverge between login creation and state/command paths.

Patch on this branch:
- customer login now resolves the registered online device first;
- the submitted `clientKey` must match the resolved Agent `DeviceId`.

### RED — One AgentDevice could have multiple effective SignalR connections

The Agent hub stored one `ConnectionId` per device, but a second connection could remain in the durable device group.

Impact:
- server-to-agent commands could be delivered more than once;
- the stored connection could point at one socket while another socket still received group messages.

Patch on this branch:
- when a new connection confirms, the previous connection is removed from the device command group before the new one becomes effective.

### RED — Session transfer moved only the Station

`/api/sessions/{sessionId}/transfer` previously changed `Session.StationId` and Station states only.

Impact:
- `Session.AgentDeviceId` could still point to the old PC;
- `Session.CustomerLoginId` could still point to the old client login;
- later Agent-owned launch/stop or customer-session checks could operate against the wrong device.

Patch on this branch:
- destination must have an active Agent;
- destination Agent must be online;
- Session Agent ownership is moved to the destination;
- an active customer login on the destination is adopted when present;
- the old customer login is released when ownership moves away.

## Findings re-checked and NOT blindly re-patched

The current code already contains fixes for several older findings:
- Pending settlement selection filters `AccountState == PendingPayment`.
- Pending settlement preview uses the pending customer-account state.
- Buffet sale for normal session/customer flows checks Showcase stock.
- Pending buffet sale explicitly requires a PendingPayment customer account.

These are treated as regression targets, not fresh defects.

## Remaining high-priority validation targets

1. Concurrent transfer to the same destination station.
2. Duplicate SignalR connection regression with two live sockets.
3. Pairing-code replay against an existing DeviceId.
4. Registration brute-force/rate-limit behavior.
5. Customer A attempting Customer B state access from the same Client.
6. Customer login with a forged/non-matching ClientKey.
7. Session transfer with target Agent online but no target customer login.
8. Session transfer with target Agent offline.
9. Invoice reverse restoring Showcase inventory rather than Warehouse inventory.
10. VIP usage/reporting against billable time after pause/adjustment/cancel.
11. Mixed money+minutes free-benefit ledger semantics.
12. Audit actor preservation for every sensitive Session change.

## Validation gate

The branch is not a release and must not be merged as-is.

Required acceptance sequence on the user's Windows self-hosted runner:
- full `dotnet test`;
- Server build;
- EF/migration validation;
- Server production smoke;
- Dashboard build;
- Agent registration/heartbeat smoke;
- duplicate SignalR connection test;
- customer-auth ownership test;
- Session transfer concurrency test;
- real Server + Client install smoke.

A finding is considered closed only when code + regression + real required validation all pass on the same SHA.
