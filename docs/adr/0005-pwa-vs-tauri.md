# ADR 0005: Web-first, not PWA or Tauri

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Cashiers need a register UI on tablets and desktops. Two packaging options came up: an installable PWA, and a Tauri desktop shell around the same UI. Both add a release channel (service worker, store signing, auto-update, offline app shell) on top of the POS that already ships as Expo (web and native) and the Admin panel that already ships as Next.js.

The production decision is one shared POS origin (`pos.regkasse.at`) and one Admin origin (`admin.regkasse.at`). Tenant comes from the JWT, not from an installed bundle.

## Decision

Do not pursue a PWA or a Tauri shell in this phase. Stay web-first.

- POS: Expo web on `pos.regkasse.at`, plus the existing native client when a device build is required.
- Admin: Next.js on `admin.regkasse.at`.
- Customer websites stay in `frontend-sites`. Working hours and online orders apply there. They are not a cashier PWA.

No service-worker offline shell and no Tauri process will be added to stand in for the cash register. Offline queues that already exist (TSE intents and order snapshots) remain server-reconciled queues, not an installed app runtime.

## Consequences

- Browser updates ship the cashier and Admin UI. There is no desktop-shell version to keep in lockstep.
- Install prompts, Tauri IPC, and a custom protocol handler are out of scope.
- Revisit this only when a store build or an offline shell is an explicit product requirement. Until then, new cashier features land in the Expo POS and new back-office features land in Admin.
- This rejection does not remove the native POS binary or the customer website.
