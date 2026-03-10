# Follow-up: Final role taxonomy simplification

**Status:** Backlog — not in scope of the role-governance hardening PR.

## Why deferred

- Current code defines **system role** = membership in `Roles.Canonical`. Changing that list (e.g. removing Manager) affects seed, matrix, every assigned user, and admin UI — must be a dedicated rollout.
- Governance PR intentionally keeps **compatibility-first**: lock mutability, Demo-as-flag, sync fixes, drawer stability — without collapsing role count in the same merge.

## Target minimal model (long-term)

- **SuperAdmin** — tenant break-glass, user lifecycle
- **Operator** — floor POS (replaces overlapping Cashier/Waiter as separate tiers if desired)
- **Backoffice** — catalog, reports, non-fiscal config
- **Kitchen** — station-only surface

(Exact matrix and migration order TBD.)

## Re-evaluation candidates

| Current canonical | Question for follow-up |
|-------------------|-------------------------|
| **Manager** | Consolidate into Backoffice? Or stay canonical but with preset-only differentiation? |
| **ReportViewer** | Custom role + preset, or stay read-only tier? |
| **Accountant** | Merge with Backoffice permission set or keep separate for compliance? |
| **Cashier / Waiter** | Remain distinct roles or become Operator + preset/workflow profile? |

## Deliverables when picked up

1. Decision doc: which names stay in `Roles.Canonical` vs become custom/preset-only.
2. Data migration: reassign users; remove deprecated `AspNetRoles` rows only after zero assignments.
3. `RolePermissionMatrix` + seed alignment.
4. Admin FE role lists and any hardcoded role arrays.

## References

- `ai/POS_ROLE_ARCHITECTURE_IDEAL.md`
- `ai/LEGACY_ROLE_MIGRATION_PLAN.md`
- `backend/Authorization/Roles.cs` — `Canonical` comment documents compatibility boundary.
