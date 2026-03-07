# Regkasse Add-on Stabilization – Production Readiness Summary

**Date:** 2025-03-07  
**Scope:** Legacy Modifier Migration Tool, Add-on System Stabilization, POS Add-on UX Improvement

---

## 1. What Is Completed

### 1.1 Architecture (Add-on = Product)

| Area | Status | Notes |
|------|--------|-------|
| Add-on = Product | ✅ | `IsSellableAddOn`, `AddOnGroupProduct`; add-ons are normal products |
| Legacy modifier = compatibility only | ✅ | Read-only; no new writes; migration creates Products |
| Cart flow | ✅ | Add-ons as separate cart lines; no `CartItemModifier` for new add-ons |
| Payment flow | ✅ | One `PaymentItem` per cart line; flat structure |
| Receipt flow | ✅ | One `ReceiptItem` per `PaymentItem`; no nested modifier lines |
| Table order | ✅ | One `TableOrderItem` per cart item; no new `TableOrderItemModifier` |

### 1.2 Legacy Modifier Migration Tool

| Item | Status | Notes |
|------|--------|-------|
| `MigrateSingleByModifierIdAsync` | ✅ | Transactional (PostgreSQL); atomic Product + AddOnGroupProduct + modifier deactivation |
| `MigrateLegacyModifier` controller | ✅ | Pre-validates `groupId` before migration; uses transactional service |
| Batch `MigrateAsync` | ✅ | Per-modifier atomic (Product + link in one SaveChanges); idempotent; dry-run |
| Admin "Als Produkt migrieren" | ✅ | Single modifier migration with category selection |
| Admin batch migration | ✅ | `POST /api/admin/migrate-legacy-modifiers` with dryRun |

### 1.3 Add-on System Stabilization

| Item | Status | Notes |
|------|--------|-------|
| `AddProductToGroup` | ✅ | ProductId or CreateNewAddOnProduct; Price ≥ 0 validated |
| `CreateNewAddOnProductRequest` | ✅ | Name required; CategoryId validated in controller |
| Barcode uniqueness | ✅ | `ADDON-{guid}` format; no collisions |
| Modifier creation frozen | ✅ | `POST .../modifiers` returns 410 Gone |

### 1.4 POS Add-on UX

| Item | Status | Notes |
|------|--------|-------|
| `ModifierSelectionBottomSheet` | ✅ | Fetch error state; "Fehler beim Laden der Extras" |
| `ModifierSelectionModal` | ✅ | Fetch error state; same error message |
| `ProductRow` / `ProductGridCard` | ✅ | `group.products` primary; `group.modifiers` legacy fallback |
| `handleAddAddOn` | ✅ | `addItem(productId, 1, { productName, unitPrice })` |
| `useProductModifierGroups` | ✅ | Cached fetch; loading/error handling |

### 1.5 Fixes Applied in This Review

| Fix | Location | Description |
|-----|----------|-------------|
| Pre-validate groupId | `ModifierGroupsController.MigrateLegacyModifier` | Validate modifier belongs to group before migration; avoid migrate-then-reject |
| Fetch error handling | `ModifierSelectionBottomSheet`, `ModifierSelectionModal` | Show "Fehler beim Laden" instead of "Keine Extras" when API fails |
| Price validation | `ModifierGroupsController.AddProductToGroup` | Reject `Price < 0` for new add-on products |

---

## 2. What Is Still Risky

### 2.1 Migration

| Risk | Severity | Mitigation |
|------|----------|------------|
| Batch `MigrateAsync` has no transaction | Medium | Each modifier is atomic (Product + link in one SaveChanges). Partial batch success is intentional. For strict all-or-nothing, wrap loop in transaction (optional). |
| Idempotency relies on Name+Price match | Low | Same-name, same-price in same group = skip. Edge case: manual product with same name/price could cause skip. |
| Inactive modifier without matching product | Low | `MigrateSingleByModifierIdAsync` throws; admin must fix manually. |

### 2.2 POS / Cart

| Risk | Severity | Mitigation |
|------|----------|------------|
| `productId` vs `itemId` in cart display | Low | `_layout.tsx` uses `itemId ?? clientId ?? productId`; ensure consistency across flows. |
| Legacy carts with modifiers | Low | BuildCartResponse maps `CartItemModifiers` to `SelectedModifiers`; read-only. |
| Offline / PouchDB | Not in scope | Add-on flow assumes online; offline sync not verified. |

### 2.3 Admin

| Risk | Severity | Mitigation |
|------|----------|------------|
| New product form in modifier-groups | Low | Name required (DTO); CategoryId, Price validated in controller. Frontend form rules should match. |
| Duplicate product in group | Low | `AddProductToGroup` checks `AddOnGroupProducts`; returns 409 if already in group. |

---

## 3. What Should Be Done Before Production Rollout

### 3.1 Mandatory

| Task | Owner | Notes |
|------|-------|-------|
| Run migration in staging with dryRun first | Ops | Verify counts; fix any category/group issues |
| Run single migration for one modifier | Ops | Confirm "Als Produkt migrieren" end-to-end |
| Verify POS add-on flow on device | QA | Add product with extras; pay; check receipt |
| Verify legacy cart load | QA | Load cart with old modifiers; ensure no crash |
| Verify receipt for legacy payment | QA | Payment with Modifiers in JSON; receipt renders |

### 3.2 Recommended

| Task | Owner | Notes |
|------|-------|-------|
| Add integration test for `MigrateLegacyModifier` | Dev | Assert transactional rollback on failure |
| Add integration test for batch migration idempotency | Dev | Run twice; assert SkippedCount on second run |
| Smoke test modifier-groups admin page | QA | Create group, add product, migrate one modifier |

---

## 4. What Can Wait for a Later Cleanup Phase

| Item | Prerequisite | Notes |
|------|--------------|-------|
| Remove `MigrateSingleAsync` (keep only `MigrateSingleByModifierIdAsync`) | No callers | Dead code cleanup |
| Stop including `g.Modifiers` in API response | All groups migrated | Reduces payload; legacy POS must be updated |
| Remove `SelectedModifiers` from AddItemToCartRequest | FE never sends | Already ignored; schema cleanup |
| Remove `ProductModifierValidationService` modifier path | No code uses it | PaymentService block is dead |
| Add retry button to ModifierSelection error state | UX improvement | "Erneut versuchen" triggers refetch |
| Batch migration transaction (all-or-nothing) | If strict requirement | Currently per-modifier atomic; optional |

---

## 5. Architecture Rule Verification

**Rule:** Add-on = Product; legacy modifier = compatibility layer only.

| Check | Result |
|-------|--------|
| New add-ons created as Products | ✅ `AddProductToGroup` creates Product + AddOnGroupProduct |
| Legacy modifiers read-only | ✅ No new modifier creation; migration creates Products |
| Cart lines for add-ons | ✅ Separate cart lines; no CartItemModifier for add-ons |
| Payment items for add-ons | ✅ One PaymentItem per cart line |
| Receipt lines for add-ons | ✅ One ReceiptItem per PaymentItem |
| Legacy modifiers in response | ✅ `group.modifiers` still returned for backward compat |
| Legacy modifier selection in POS | ✅ Fallback when `group.products` empty; `onApply` for modifiers |

---

## 6. File Reference

| Area | Path |
|------|------|
| Migration service | `backend/Services/ModifierMigrationService.cs` |
| Migration controller | `backend/Controllers/ModifierGroupsController.cs` |
| Admin migration | `backend/Controllers/AdminMigrationController.cs` |
| Modifier DTOs | `backend/DTOs/ModifierDTOs.cs` |
| POS add-on handler | `frontend/app/(tabs)/cash-register.tsx` |
| Modifier selection UI | `frontend/components/ModifierSelectionBottomSheet.tsx`, `ModifierSelectionModal.tsx` |
| Cart context | `frontend/contexts/CartContext.tsx` |
| Deprecation doc | `docs/architecture/addon-refactor/LEGACY_MODIFIER_DEPRECATION.md` |
| Validation checklist | `docs/architecture/addon-refactor/ADDON_VALIDATION_CHECKLIST.md` |
