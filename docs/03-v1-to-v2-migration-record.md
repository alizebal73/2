# v1 → v2 Migration Record

## Protected source

Source repository:
`alizebal73/2`

Stable line:
`main`

At architecture planning time the stable baseline observed was:
`ad38ca91ef9abe08ae56d70beb8c8ebb82fbf777`

The current security/SQLite release work is still treated as pre-release until its exact SHA has green CI and the installer/physical gates are complete.

## Non-negotiable

The v2 project must never silently change the meaning of:
- money
- paid amount
- free benefit
- debt
- wallet balance
- session duration
- active login ownership
- station ownership
- game launch authorization
- account lease ownership
- inventory stock
- shift totals

without an explicit migration decision and regression coverage.

## Compatibility source of truth

The following v1 documents must be treated as reference inputs when v2 starts:
- docs/00-index.md
- docs/03-architecture-plan.md
- docs/04-tech-stack-roadmap.md
- docs/07-information-architecture.md
- docs/08-action-map.md
- docs/09-client-experience.md
- docs/10-product-completion-backlog.md
- docs/20-physical-validation-2-3pc-2026-10-05.md
- docs/21-release-preflight-2026-10-05.md

## v2 startup procedure

1. Capture final v1 release SHA.
2. Capture final database/schema version.
3. Capture installer versions.
4. Capture acceptance scenarios from physical validation.
5. Create v2 repository.
6. Copy only approved contracts/behaviors, not the monolithic implementation.
7. Start Stations vertical slice.
8. Establish architecture tests.
9. Continue module by module.

## Definition of done for migration

A migrated feature is not complete until:
- behavior parity is documented
- new structure is isolated
- domain tests exist
- API integration tests exist
- auth tests exist
- persistence tests exist
- relevant E2E exists
- physical implications are checked if PC/Agent-related
- old release remains untouched
