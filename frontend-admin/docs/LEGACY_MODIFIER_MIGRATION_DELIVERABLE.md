# Legacy Modifier Migration Workflow – Deliverable

## What was changed

1. **Dedicated migration API module**
   - Added `src/lib/api/legacyModifierMigration.ts` for the admin migration workflow only. It exposes:
     - `getMigrationProgress()`: GET /api/admin/migration-progress (returns active legacy modifier count and groups-with-modifiers-only count).
     - `runBulkMigration(body)`: POST /api/admin/migrate-legacy-modifiers (defaultCategoryId, dryRun). Returns a result DTO with totalProcessed, migratedCount, skippedCount, errorCount, migrated[], skipped[], errors[].
   - Response handling is defensive: unwraps backend `{ data, message, success }` envelope and supports both camelCase and PascalCase property names.

2. **Migration progress view**
   - On the modifier-groups page, a card at the top shows:
     - **Aktive Legacy-Modifier**: count from GET /api/admin/migration-progress.
     - **Gruppen nur mit Legacy-Modifiern**: count from the same endpoint.
   - Data is loaded with `useQuery` (key: `['admin', 'migration-progress']`). Loading state is shown in the statistics.

3. **Bulk migration (clearly separate from single)**
   - **Trigger**: Button "Bulk-Migration ausführen" in the same card opens a dedicated bulk modal.
   - **Flow**: User selects default category (required), optionally enables "Nur Bericht (keine Änderungen)" (dry run), and must check "Ich bestätige: Alle aktiven Legacy-Modifier werden migriert. Teilfehler werden gemeldet." to enable the submit button. On submit, `runBulkMigration` is called; loading state is shown; on success the modal shows the result (total/migrated/skipped/error counts and, if any, the list of errors). Refetch of modifier-groups and migration-progress runs after a non–dry-run run.
   - **Feedback**: Loading (button), success/warning message (e.g. partial failure), and result screen inside the modal with error details. No hidden or irreversible action without confirmation.

4. **Single modifier migration (unchanged endpoint, clearer confirmation)**
   - Single migration still uses the existing flow: "Als Produkt migrieren" per legacy modifier → modal with category and "Legacy-Modifier nach Migration deaktivieren". No change to the API call (still POST /api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate).
   - **Explicit confirmation**: A checkbox was added in the single-migrate modal: "Migration bestätigen: Add-on-Produkt anlegen und ggf. Legacy-Modifier deaktivieren." The "Migrieren" button is disabled until this is checked.
   - Code is clearly separated from bulk: comments and state distinguish "Legacy modifier migration: progress + bulk" from "Single modifier migration".

5. **Loading, success, failure, partial-failure feedback**
   - **Progress**: Loading state in the progress card statistics.
   - **Bulk**: Button shows loading during run; success/warning message via `message.success` / `message.warning`; result screen in modal with error list for partial failure; defensive display of error items (camelCase/PascalCase).
   - **Single**: Existing `confirmLoading` and `message.success` / `message.error`; no change beyond the confirmation checkbox.

6. **Defensive backend response handling**
   - `getMigrationProgress` and `runBulkMigration` unwrap the backend success envelope and normalize numeric and array fields. They tolerate missing or differently cased properties (e.g. PascalCase from backend). Error list items are displayed with fallbacks for modifierName/reason.

7. **Technical artifacts in English**
   - Page and new module use English comments and identifiers. UI copy remains German where it already was (product requirement).

---

## Architecture and boundaries

- **Migration API**: Migration workflow uses `src/lib/api/legacyModifierMigration.ts` only for progress (GET /api/admin/migration-progress) and bulk migration (POST /api/admin/migrate-legacy-modifiers). Legacy endpoints are not used directly; the modifier-groups page does not import `@/api/legacy/*` for migration.
- **Single migration**: Still uses `src/lib/api/modifierGroups.ts` (`migrateLegacyModifier` → POST .../modifier-groups/.../modifiers/.../migrate). Catalog domains (products, categories) do not depend on the migration module.
- **Admin catalog**: Products and categories domains remain independent; they use `@/api/admin/*` only. Migration is a separate workflow on the modifier-groups page.

---

## Files modified

| File | Change |
|------|--------|
| `src/lib/api/legacyModifierMigration.ts` | **New.** Migration progress and bulk migration API; defensive unwrap and property names. |
| `src/app/(protected)/modifier-groups/page.tsx` | Migration progress card; bulk modal (category, dry run, confirmation checkbox, result view); single-migrate confirmation checkbox; comments separating bulk vs single; imports for migration API. |
| `docs/LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md` | **New.** This deliverable. |

**Not modified**
- `src/lib/api/modifierGroups.ts` – single modifier migration still uses existing `migrateLegacyModifier` (POST .../modifier-groups/.../modifiers/.../migrate). No change to backend contracts.

---

## Remaining backend contract risks

- **Success envelope**: All admin endpoints may return `{ success, message, data }`. The new module unwraps `data`; if an endpoint returns the payload at the root, the code still tries `res.data?.data ?? res.data`. If future endpoints use a different envelope, the unwrap helper may need to be extended.
- **Bulk result shape**: ModifierMigrationResultDto is assumed (totalProcessed, migratedCount, skippedCount, errorCount, migrated, skipped, errors). If the backend adds or renames fields, the frontend will ignore them or show 0/empty until the types and display are updated.
- **Progress endpoint**: GET /api/admin/migration-progress returns activeLegacyModifiersCount and groupsWithModifiersOnlyCount. Any change in semantics (e.g. including inactive modifiers in a count) would require frontend and runbook updates.
- **Single migration**: Still called via modifier-groups route (POST .../modifier-groups/{groupId}/modifiers/{modifierId}/migrate). The admin route POST /api/admin/modifiers/{id}/migrate-to-product exists but is not used by the frontend; if the modifier-groups route is deprecated, the frontend must switch and pass the same payload.

---

## Business-risk notes

- **Bulk migration is best-effort**: Partial success is by design. Some modifiers may be migrated and others may fail; failures are reported in the result. Operators should review the error list and, if needed, fix data or run single migration for failed items. The UI does not auto-retry failed items.
- **Bulk does not deactivate legacy modifiers**: Only the single-modifier migration path can mark the legacy modifier inactive. After bulk migration, legacy modifiers remain active for compatibility until single migration (with "Legacy-Modifier nach Migration deaktivieren") is used or the backend adds a bulk deactivation option.
- **Dry run**: "Nur Bericht" performs no writes and returns the same result shape (counts and lists). Operators can use it to see what would be migrated/skipped/errored before running for real.
- **Idempotency**: Both single and bulk are idempotent (already-migrated modifiers are skipped). Re-running bulk or single does not create duplicate add-on products for the same modifier.

---

## Manual QA checklist

- [ ] **Progress view**: Open modifier-groups page; progress card shows "Aktive Legacy-Modifier" and "Gruppen nur mit Legacy-Modifiern" (or 0). Values update after refetch (e.g. after a migration).
- [ ] **Bulk – dry run**: Click "Bulk-Migration ausführen"; select category; leave "Nur Bericht" on; check confirmation; click "Ausführen". Modal shows result (total/migrated/skipped/error); no modifier-groups data changed.
- [ ] **Bulk – real run**: Select category; turn off dry run; check confirmation; run. Modal shows result; progress card and group list refetch; success or warning message appears. If there are errors, error list is visible in the modal.
- [ ] **Bulk – no confirmation**: Submit button is disabled until the confirmation checkbox is checked.
- [ ] **Single migration**: Expand a group with a legacy modifier; click "Als Produkt migrieren"; "Migrieren" is disabled until the confirmation checkbox is checked. Select category, optionally set "Legacy-Modifier nach Migration deaktivieren", check confirmation, submit. Success message; modal closes; group list refetches; new add-on product appears in the group.
- [ ] **Single – cancel**: Open single migrate modal; cancel; reopen and confirm checkbox is reset.
- [ ] **Partial failure (if testable)**: Cause one modifier to fail (e.g. invalid category or backend error); run bulk. Result shows errorCount > 0 and error list; successful items are still migrated.

---

## Recommended next step

- **Operational**: Document in a short runbook when to use bulk vs single (e.g. bulk for initial sweep, single for remaining or failed items and when deactivating the legacy modifier is required).
- **Optional**: If the backend standardizes on camelCase for all admin JSON, the defensive PascalCase handling in the new module could be simplified after confirmation.
- **Optional**: Add a link or hint from the progress card to the legacy-modifier section (e.g. "In den Gruppen unten können Sie einzelne Modifier migrieren.") so operators see the connection between progress and the per-group single migration actions.
