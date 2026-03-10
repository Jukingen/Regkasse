# Engineering note: Role governance PR — scope and non-goals

## This PR does **not** implement the final ideal taxonomy

The long-term target (e.g. SuperAdmin / Operator / Backoffice / Kitchen) is documented separately. **This merge does not shrink or redesign `Roles.Canonical`.**

## What this PR **does** aim to do (compatibility-first)

1. **Close system-role mutability** — canonical roles are matrix-only at runtime; PUT permissions on system roles returns `SystemRoleNotEditable`; resolver ignores claims on canonical roles.
2. **Remove Demo as an Identity role** — reserved name + migration to Cashier + `IsDemo`; `DemoUserHelper` / POS `PermissionHelper.setIsDemoUser` align on flag, not role name.
3. **Fix AspNetUsers.role vs AspNetUserRoles drift** — `AdminUsersController.Patch` syncs Identity roles when `user.Role` changes.
4. **Stabilize RoleManagementDrawer** — effect deps avoid unstable `roles` reference loops; system role UI read-only matches backend.

## Why Manager remains canonical / system in this PR

- **Manager** is in `Roles.Canonical`; `IsSystemRole` is **only** `Canonical.Contains(...)`. So Manager is immutable/non-deletable/matrix-only like SuperAdmin — **by design of the current list**, not by a special case.
- **Leaving Manager in Canonical** avoids a breaking migration and matrix churn in the same PR as governance locking.
- **Reclassifying Manager** (custom vs preset vs consolidated role) is **follow-up** — see `ai/FOLLOWUP_ROLE_TAXONOMY_SIMPLIFICATION.md`.

## Rollout boundary

- **In scope now:** behavior and docs above; no removal of Manager (or other canonical) from the list.
- **Out of scope now:** reducing canonical count, renaming canonical roles, or merging Manager into another tier without migration.

## Reviewer checklist

- [ ] No change to `Roles.Canonical` array membership in this PR (unless explicitly agreed as separate migration).
- [ ] Seed still creates only missing canonical names; legacy cleanup remains migration-only.
- [ ] Follow-up doc exists for taxonomy simplification.
