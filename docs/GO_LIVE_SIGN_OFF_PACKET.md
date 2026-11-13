# GO_LIVE sign-off packet — send for human review

**Prepared:** 2026-08-17 (UTC)  
**Checklist:** [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md)  
**Status:** **Ready for review — not signed.** An agent cannot act as Technical Lead, Operations, ComplianceOfficer, or Product Owner.

Print or attach this file plus the evidence list. Recipients sign [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) §8 (and §5.3) only after they have reviewed **host** evidence, not only this repository.

## Recipients (print name — do not pre-fill)

| Role | Name | How to send |
|------|------|-------------|
| Technical Lead / Engineering | ________________________ | Internal review + this packet |
| Operations | ________________________ | Host evidence: DNS, volumes, backup dump, Alertmanager render |
| ComplianceOfficer | ________________________ | TSE LIVE SCU, FON Real, AVV, fiscal smoke |
| Product Owner / Founder | ________________________ | Pilots, SLA, go-live date |

**Circulation:** copy this packet + [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) to the four roles (ticket, signed PDF, or email). Collect wet-ink or named digital signatures on §8. Until then the decision is **NO-GO**.

## What this packet is

A **review pack**, not a completed go-live. Sections in the main checklist stay unticked where the work is on the production host or requires a named human.

## Conditions / caveats (must be closed before GO)

| ID | Condition | Status as of 2026-08-17 |
|----|-----------|-------------------------|
| C1 | `ASPNETCORE_ENVIRONMENT=Production` on `api.regkasse.at` | Host only |
| C2 | Fiskaly **LIVE** org + SCU + FON auth (tid / benid / pin) — secrets not in git | See [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md) — **not executed here** |
| C3 | FinanzOnline `UseSimulation=false`, `RksvSubmission:ClientKind=Real` | Host secrets |
| C4 | TSE `TseMode=Device`, `Mode=Real`, `Provider=fiskaly` (no Soft/Demo/Fake) | Startup lock in repo; live keys on host |
| C5 | Isolated System backup restore drill **Passed** | [`BACKUP_RESTORE_DRILL_EVIDENCE.md`](BACKUP_RESTORE_DRILL_EVIDENCE.md) — **not executed** (no dump on this workstation) |
| C6 | Alertmanager **rendered** receivers (Slack `#regkasse-alerts`, email `ops@regkasse.at`) + acknowledged test alert | Tracked file still **null**; AM not running locally |
| C7 | CSRF, SuperAdmin 2FA, Redis, backup `PgDump` | Repo fail-closed; host values required |
| C8 | AVV signed for first paying pilots | Legal / ComplianceOfficer |
| C9 | On-call named | Operations |
| C10 | `REGKASSE_DEPLOY_CONFIRM=YES ./scripts/ops/deploy-production.sh` only on the **Linux production host** after C1–C9 | **Do not run from a developer laptop** |

## Repo vs host (honest split)

| Done in repo (engineering) | Still required on host / by humans |
|----------------------------|--------------------------------------|
| Production config guard, CSRF default on, `/metrics` ACL, POS log gating, EF snapshot sync | DNS/TLS, durable volumes, JWT rotation |
| Fiskaly LIVE template keys (`Environment`, `ScuId` aliases, SIGN AT URL) | Fiskaly Dashboard LIVE SCU + API keys |
| FON cutover checklist | Real SOAP credentials |
| Alertmanager **example** + render script | Mount rendered YAML; fire test alert |
| Restore drill **procedure** + evidence log | `pg_restore` of a Succeeded System dump into an isolated DB |
| Deploy script + preflight + smoke docs | Signed §8, then deploy on the server |

## Recommendation

**NO-GO** until §8 is signed and C1–C10 are evidenced. Signing this packet without host proof does not authorize fiscal Production traffic.
