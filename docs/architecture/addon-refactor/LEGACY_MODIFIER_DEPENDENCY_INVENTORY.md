# Legacy Modifier / Compatibility / Migration Layer — Complete Dependency Inventory

**Date:** 2025-03-07  
**Scope:** Full repo scan for removal of legacy modifier, compatibility, bulk migration, migration progress, migrated badges, and legacy-only UI/API/service/DB.  
**Constraints:** Keep Product + Add-on Group + Add-on Product domain model; do not break POS runtime, receipt generation, pricing, VAT/tax, reporting, or daily closing.  
**Code terms:** English only.

---

## 1. Frontend files and exact usages

### 1.1 Frontend-admin (Next.js 14, TypeScript, AntD, React Query)

| File | Lines | Usage |
|------|-------|--------|
| `frontend-admin/src/lib/api/legacyModifierMigration.ts` | 1–115 | **Entire file.** Legacy modifier migration workflow: `getMigrationProgress()` (GET `/api/admin/migration-progress`), `runBulkMigration()` (POST `/api/admin/migrate-legacy-modifiers`), DTOs: `MigrationProgressDto`, `BulkMigrationRequestDto`, `BulkMigrationResultDto`, `MigrationItemDto`, `MigrationErrorDto`. |
| `frontend-admin/src/lib/api/modifierGroups.ts` | 15 | `ModifierDto`: comment "false when migrated/deactivated (legacy modifier after migration)". |
| | 36 | Comment "Legacy (Fallback)". |
| | 48 | Comment: "Use for migration UI and for group list in product form. Phase D PR-D: legacy modifiers only here." |
| | 91–100 | `addLegacyModifierToGroup`: deprecated, throws "Legacy modifier creation is disabled (410). Use addProductToGroup instead." |
| | 126–152 | `MigrateLegacyModifierBody`, `MigrateLegacyModifierResult`, `migrateLegacyModifier()` → POST `/api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate`. |
| `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | 4–6 | Comment: "legacy modifier migration workflow". |
| | 19 | Import `migrateLegacyModifier`. |
| | 24 | Import `getMigrationProgress`, `runBulkMigration`, `BulkMigrationResultDto` from `legacyModifierMigration`. |
| | 30 | `migrationProgressKey = ['admin', 'migration-progress']`. |
| | 50–54 | State: bulk modal, bulk loading, bulk result, bulk confirm, bulk form (legacy migration). |
| | 55–59 | `useQuery({ queryKey: migrationProgressKey, queryFn: getMigrationProgress })`. |
| | 139–167 | `handleBulkMigration`: calls `runBulkMigration`, refetches modifier-groups and migration-progress. |
| | 168–174 | `closeBulkModal`: refetches migration-progress. |
| | 178–210 | Single modifier migration: `openMigrateModal`, `handleMigrateModifier` (calls `migrateLegacyModifier`), success message "Legacy-Modifier wurde als Produkt migriert". |
| | 312–321 | UI: "Legacy-Modifier (Kompatibilität)" section, "Keine Legacy-Modifier." empty state. |
| | 364–366 | Hint: "Legacy-Modifier dienen nur der Kompatibilität." |
| | 369–393 | Progress card: "Aktive Legacy-Modifier", "Gruppen nur mit Legacy-Modifiern", "Bulk-Migration ausführen" button. |
| | 511–545 | Single migrate modal: "Als Produkt migrieren", "Legacy-Modifier nach Migration deaktivieren", confirmation checkbox. |
| | 539–608 | Bulk migration modal: title "Bulk-Migration: Legacy-Modifier → Add-on-Produkte", form (category, dry run, confirmation), result display (migratedCount, skippedCount, errorCount, errors). |
| `frontend-admin/src/features/products/components/ExtraZutatenSection.tsx` | 16 | Comment: "modifiers only for legacy display subsection". |
| | 53 | Comment: "Legacy-only: modifiers come from getModifierGroups()". |
| | 85 | UI label: "Modifier (Legacy, nur Leseansicht)". |

**Excluded (different “legacy”):** `frontend-admin/src/api/legacy/payment.ts`, `frontend-admin/src/app/(protected)/payments/page.tsx`, `frontend-admin/src/lib/axios.ts` (legacy path warning), `frontend-admin/src/api/admin/products.ts` (comment only). These refer to legacy Payment/Cart API boundary, not modifier migration.

### 1.2 Frontend POS (Expo, React Native)

| File | Lines | Usage |
|------|-------|--------|
| `frontend/services/api/productModifiersService.ts` | 8–9 | Comment and `ModifierDto`: "Shape for group.modifiers. POS endpoints return empty array; type kept for DTO shape and admin compat." |
| | 45 | Comment: "Admin endpoints still return .modifiers for legacy display/migration." |
| | 57 | `ModifierGroupDto.modifiers`: "Legacy modifier list. Empty from POS endpoints (Phase D PR-C); admin endpoints may still populate. Do not use for POS add-on display." |
| | 79, 89 | `mapGroupForPOS`: sets `modifiers: []` (guard: .modifiers must not be used for POS). |
| `frontend/hooks/useProductModifierGroups.ts` | 47 | Comment: "Phase C: add-ons only from group.products (legacy group.modifiers fallback removed)." |
| `frontend/components/ProductRow.tsx` | 52 | Comment: "Phase C: only groups with add-on products (group.products). Legacy group.modifiers removed." |
| `frontend/components/ProductGridCard.tsx` | 50 | Same comment. |
| `frontend/components/ModifierSelectionBottomSheet.tsx` | 111–138 | Uses `group.products ?? []` only for option rows; no `group.modifiers` in render (Phase C compliant). |
| `frontend/contexts/CartContext.tsx` | 46, 211, 223–224, 313–314, 327, 345, 352, 407, 421, 604, 655, 662, 704, 731, 762, 789, 819, 861, 930 | **Cart line `item.modifiers`**: used for display, totals, merge/dedupe, line keys. These are **per-cart-line embedded modifiers** (shape from API `SelectedModifiers` / denormalized). Required for **runtime**: legacy carts and table orders that still have embedded modifiers must display and total correctly. **Refactor-required**: keep reading `item.modifiers`/SelectedModifiers for existing data; do not remove until no legacy cart/order has modifiers. |
| `frontend/components/CartItemRow.tsx` | 53–55, 102 | Renders `item.modifiers` for display and modifier total. |
| `frontend/components/CartDisplay.tsx` | 99 | `item.modifiers` for line key. |
| `frontend/components/ExtrasChips.tsx` | 28 | `modifiers?.length` (display). |
| `frontend/app/(tabs)/cash-register.tsx` | 178 | `item.modifiers` in map. |
| `frontend/app/(tabs)/_layout.tsx` | 126 | `item.modifiers` mapped to modifierId/name/priceDelta (likely sync/recovery). |
| `frontend/components/ReceiptLineItem.tsx` | 39 | `styles.modifiers` (presentation only). |
| `frontend/components/ReceiptSummary.tsx` | 62, 83–85 | `item.modifiers` for grouping and display. |

---

## 2. Backend controllers / endpoints

| Controller | Method | Route | Legacy / migration usage |
|------------|--------|-------|---------------------------|
| **AdminMigrationController** | GetMigrationProgress | GET `api/admin/migration-progress` | Returns `LegacyModifierMigrationProgressDto` (activeLegacyModifiersCount, groupsWithModifiersOnlyCount). **Legacy-only.** |
| | MigrateLegacyModifiers | POST `api/admin/migrate-legacy-modifiers` | Batch migration; calls `IModifierMigrationService.MigrateAsync`. **Legacy-only.** |
| | MigrateModifierToProduct | POST `api/admin/modifiers/{modifierId}/migrate-to-product` | Single migration; calls `MigrateSingleByModifierIdAsync`. **Legacy-only.** |
| **ModifierGroupsController** | GetAll | GET `api/modifier-groups` | `.Include(g => g.Modifiers)`; `MapToModifierGroupDto` fills DTO.Modifiers; logs `Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers`. **Refactor:** stop Include(Modifiers) and set Modifiers = [] for removal. |
| | GetById | GET `api/modifier-groups/{id}` | Same. |
| | AddModifier | POST `api/modifier-groups/{groupId}/modifiers` | Returns 410 Gone (legacy modifier creation frozen). **Safe to remove or keep 410.** |
| | MigrateLegacyModifier | POST `api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate` | Single migration; calls `MigrateSingleByModifierIdAsync`. **Legacy-only.** |
| **AdminProductsController** | GetProductModifierGroups | GET `api/admin/products/{id}/modifier-groups` | Loads groups with Include; `MapToModifierGroupDto` (with Modifiers) and `MapToModifierGroupDtoForAdminProduct` (Modifiers = empty). **Refactor:** use only ForAdminProduct or stop filling Modifiers. |
| | (MapToModifierGroupDto) | — | 388–416: fills Modifiers from `g.Modifiers`. |
| **ProductController** | GetCatalog | GET `api/Product/catalog` or POS catalog | Does **not** Include(Modifiers); uses `MapToModifierGroupDtoForPos` (Modifiers = []). **No change for POS.** |
| | GetProductModifierGroups | GET `api/Product/{id}/modifier-groups` | Same: no Include(Modifiers), MapToModifierGroupDtoForPos. **No change for POS.** |
| | (MapToModifierGroupDto) | — | 230–259: private method that maps g.Modifiers; used only if something called it with loaded Modifiers (currently catalog/GetProductModifierGroups do not load Modifiers). **Dead or refactor.** |
| **CartController** | Multiple | GET/POST cart, table-order recovery | `.ThenInclude(i => i.Modifiers)` for CartItem and TableOrderItem; builds `SelectedModifiers` from `item.Modifiers` (1214–1215, 1288–1289, 1477–1494); logs Phase2.LegacyModifier. **High-risk:** required for runtime for legacy carts/orders; do not remove read path until no such data. |

---

## 3. Services and interfaces

| Service / interface | Usage |
|---------------------|--------|
| **IModifierMigrationService** | `MigrateAsync`, `MigrateSingleAsync`, `MigrateSingleByModifierIdAsync`, `GetMigrationProgressAsync`. **Legacy-only;** remove with migration layer. |
| **ModifierMigrationService** | Implements above; reads/writes `ProductModifiers`, creates Products and AddOnGroupProducts. **Legacy-only.** |
| **IProductModifierValidationService** | `GetAllowedModifierIdsForProductAsync`, `GetAllowedModifiersWithPricesForProductAsync` — both query `ProductModifiers`. Used by PaymentService (legacy path, currently unreachable) and ModifierMigrationService. **Refactor:** remove or stub when ProductModifiers table/code removed. |
| **ProductModifierValidationService** | Implements above; uses `_context.ProductModifiers`. |
| **PaymentService** | 202–261: ModifierIds/Modifiers in request ignored (Phase 3); block kept for readability. 228: `GetAllowedModifiersWithPricesForProductAsync` (unreachable). 289, 1162–1190: legacy payment items with `item.Modifiers` — **read path for receipt/snapshot**. **High-risk:** receipt and totals depend on correctly handling existing PaymentItem.Modifiers. |
| **ReceiptService** | 126–147: `hasLegacyModifiers`, line/tax math for items with Modifiers. **High-risk:** receipt generation and VAT must stay correct for legacy payments. |
| **TableOrderService** | 36: `.ThenInclude(i => i.Modifiers)`. 139, 178: Phase2.LegacyModifier logs. **Refactor:** keep Include for read-only recovery; no write of new TableOrderItemModifiers. |

---

## 4. DTOs / contracts / Swagger

| DTO / contract | File | Usage |
|----------------|------|--------|
| **ModifierMigrationDTOs** | `backend/DTOs/ModifierMigrationDTOs.cs` | `ModifierMigrationResultDto`, `ModifierMigrationItemDto`, `ModifierMigrationErrorDto`, `ModifierMigrationRequestDto`, `MigrateSingleModifierRequestDto`, `MigrateSingleModifierResultDto`, `LegacyModifierMigrationProgressDto`. **Legacy-only.** |
| **ModifierDTOs** | `backend/DTOs/ModifierDTOs.cs` | `ModifierGroupDto.Modifiers` ([Obsolete]), `ModifierDto`, `CreateModifierRequest` (deprecated), `SelectedModifierDto`, `SelectedModifierInputDto`. **Refactor:** Modifiers can be always empty; SelectedModifier* kept for historical cart/order read. |
| **PaymentItemRequest** | (in Payment DTOs) | `ModifierIds`, `Modifiers` — ignored on write; keep for contract compatibility or document only. |
| **Swagger** | `backend/swagger.json` | Paths: `/api/admin/migration-progress` (9), `/api/admin/migrate-legacy-modifiers` (21), `/api/admin/modifiers/{modifierId}/migrate-to-product` (52), `/api/modifier-groups/{groupId}/modifiers` (5176), `/api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate` (5293). Schemas: ProductModifier, ModifierGroupDto with modifiers, etc. |

---

## 5. DB entities / EF configuration / migrations

| Entity / config | File | Usage |
|-----------------|------|--------|
| **ProductModifier** | `backend/Models/ProductModifier.cs` | Table `product_modifiers`. **Remove** when migration layer removed and no code reads/writes. |
| **ProductModifierGroup** | `backend/Models/ProductModifierGroup.cs` | Navigation `Modifiers` (collection of ProductModifier). **Keep** entity; remove navigation/Include when product_modifiers dropped. |
| **CartItemModifier** | `backend/Models/CartItemModifier.cs` | Denormalized modifier on cart line (modifier_id, name, price, etc.). No FK to product_modifiers. **Keep** for historical read. |
| **TableOrderItemModifier** | `backend/Models/TableOrderItemModifier.cs` | Same for table order. **Keep** for historical read. |
| **AppDbContext** | `backend/Data/AppDbContext.cs` | DbSets: `ProductModifiers` (60), `CartItemModifiers` (20), `TableOrderItemModifiers` (47). Config for CartItemModifier/TableOrderItemModifier modifier_id (756–760, 870–874). |
| **Migrations** | `backend/Migrations/20260304143812_AddProductModifiers.cs` | Creates `product_modifiers` and `product_modifier_groups`, `product_modifier_group_assignments`. **Do not revert;** add new migration to drop only `product_modifiers` when safe. |
| | `backend/Migrations/20260306152306_AddCartItemAndTableOrderItemModifiers.cs` | Creates `cart_item_modifiers`, `table_order_item_modifiers` (no FK to product_modifiers). **Keep.** |
| **AppDbContextModelSnapshot** | `backend/Migrations/AppDbContextModelSnapshot.cs` | Reflects ProductModifier, CartItemModifier, TableOrderItemModifier. |

---

## 6. Seed / demo data

| File | Usage |
|------|--------|
| `backend/Data/SeedData.cs` | `SeedProductsAsync`: seeds Products only; **no** ProductModifiers or modifier groups. |
| `backend/Data/UserSeedData.cs` | Users/roles only; no modifier data. |
| **Conclusion** | No seed or demo data creates legacy ProductModifiers. Test fixtures in ModifierMigrationServiceTests, Phase2CartFlatAddOnTests, etc. create ProductModifiers in memory. |

---

## 7. POS / runtime fallback references

| Area | Reference | Risk |
|------|------------|------|
| **Cart response** | CartController builds `SelectedModifiers` from `CartItem.Modifiers` (CartItemModifier). Legacy carts with embedded modifiers must still return these for POS to display and total. | **High:** Removing CartItemModifiers read or Include breaks existing carts. |
| **Table order recovery** | TableOrderItemModifier loaded and serialized as SelectedModifiers. | **High:** Same as cart. |
| **Payment create** | ModifierIds/Modifiers in request ignored; new payments use flat items. No new PaymentItem.Modifiers written. | **Safe.** |
| **Payment → receipt** | PaymentService (1162–1190) and ReceiptService (126–147) read `item.Modifiers` for existing payment items to compute line totals and VAT. | **High:** Required for correct receipts and daily closing for legacy payments. |
| **POS catalog / modifier groups** | ProductController GetCatalog and GetProductModifierGroups do **not** load Modifiers; MapToModifierGroupDtoForPos returns Modifiers = []. POS uses group.products only. | **Safe:** No POS fallback on group.modifiers in API. |
| **POS client** | CartContext, CartItemRow, ReceiptSummary, etc. use `item.modifiers` for display and totals. | **Refactor-required:** Keep until no legacy cart/order/payment has modifiers; then can simplify to optional empty array. |

---

## 8. Tests

| Test file | Legacy / migration usage | Classification |
|-----------|---------------------------|----------------|
| **ModifierMigrationServiceTests.cs** | Full suite: MigrateAsync_*, MigrateSingleByModifierId_*, GetMigrationProgress_*. Seeds ProductModifiers. | **Safe to remove** when migration layer removed; or keep as skipped/legacy suite. |
| **Phase2DtoCompatibilityTests.cs** | ModifierGroupDto.Modifiers, PaymentItemRequest.ModifierIds/Modifiers, AddItemToCartRequest.SelectedModifiers serialization. | **Refactor:** Update or remove when DTOs change; keep SelectedModifiers test if read path remains. |
| **Phase2ReceiptFlatTests.cs** | GetReceiptData_FromPaymentCreatedWithModifierIds_Phase3Ignores; seeds ProductModifier. | **Refactor:** Keep receipt-from-legacy-payment test; remove ProductModifier seed when table gone. |
| **Phase2TableOrderRecoveryTests.cs** | GetTableOrdersForRecovery_WithLegacyTableOrderItemModifiers_SerializesSelectedModifiers; ConvertCartToTableOrder_WithCartItemModifiers_Phase3CreatesNoTableOrderItemModifiers. Uses TableOrderItemModifier, CartItemModifier, ProductModifier. | **High-risk:** These assert legacy read path; keep until legacy data strategy is final. |
| **Phase2CartFlatAddOnTests.cs** | AddItem_* flat vs legacy; GetCart_WithLegacyCartItemModifiers_LoadsWithoutCrash. Seeds ProductModifier, CartItemModifier. | **High-risk:** Cart load with legacy modifiers must keep working. |
| **Phase2ModifierGroupProductsTests.cs** | Include(g => g.Modifiers.Where(m => m.IsActive)). | **Refactor:** Remove Include(Modifiers) when backend stops loading modifiers. |
| **Phase2PaymentFlatItemsTests.cs** | Asserts PaymentItem Modifiers count 0 for flat items. | **Safe.** |
| **PaymentModifierValidationIntegrationTests.cs** | Likely uses ModifierIds/Modifiers in request. | **Refactor or remove** when validation service/legacy path removed. |
| **ProductModifierValidationServiceTests.cs** | ProductModifiers queries. | **Remove** with ProductModifierValidationService. |
| **CatalogStructureTests.cs** | GetCatalog_GroupWithOnlyProductsNoModifiers; ModifierGroupDto with Products/Modifiers. | **Refactor:** Align with DTO changes. |
| **Frontend** | addOnFlow.test.ts, posModifierFlow.test.ts, phaseDAddItemRequest.test.ts, phaseDPaymentRequest.test.ts — comments or assertions on group.products only / legacy path removed. | **Refactor:** Update comments/assertions as needed. |

---

## 9. Safe-to-remove vs refactor-required vs high-risk classification

### Safe to remove (legacy-only; no POS/runtime impact)

- `frontend-admin/src/lib/api/legacyModifierMigration.ts` (entire file).
- All migration progress and bulk migration UI/state/handlers in `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` (progress card, bulk modal, migrationProgressKey, getMigrationProgress, runBulkMigration, handleBulkMigration, closeBulkModal).
- Single modifier migration UI and `migrateLegacyModifier` usage on modifier-groups page (single migrate modal, "Als Produkt migrieren", legacy section "Legacy-Modifier (Kompatibilität)" and related state).
- `frontend-admin/src/lib/api/modifierGroups.ts`: `addLegacyModifierToGroup`, `migrateLegacyModifier`, `MigrateLegacyModifierBody`, `MigrateLegacyModifierResult`; and comments/IsActive related to migrated legacy.
- Backend: `AdminMigrationController.cs` (entire controller).
- Backend: `ModifierGroupsController.AddModifier` (410) and `ModifierGroupsController.MigrateLegacyModifier`; and `ModifierGroupsController` usage of `Include(g => g.Modifiers)` + MapToModifierGroupDto filling Modifiers (replace with Modifiers = []).
- Backend: `ModifierMigrationService`, `IModifierMigrationService`, `ModifierMigrationDTOs.cs`, `Program.cs` migrate-legacy-modifiers CLI block.
- Backend: `ProductModifierValidationService` and `IProductModifierValidationService` (after migration layer and any PaymentService reference removed or stubbed).
- Backend: `ProductModifier` entity and `product_modifiers` table (after no code reads/writes it).
- Swagger: paths for migration-progress, migrate-legacy-modifiers, modifiers/{id}/migrate-to-product, modifier-groups/{groupId}/modifiers (POST), modifier-groups/{groupId}/modifiers/{modifierId}/migrate.
- Tests: `ModifierMigrationServiceTests.cs` (remove or skip when migration service removed); `ProductModifierValidationServiceTests.cs` when service removed.

### Refactor-required (behavior must stay; implementation can change)

- **Admin modifier-groups page:** Remove progress/bulk/single migration and legacy subsection; keep group list, add/edit group, add product to group.
- **Admin ExtraZutatenSection:** Remove "Modifier (Legacy, nur Leseansicht)" subsection and any display of modifiers from getModifierGroups; keep only products.
- **ModifierGroupsController:** Stop Include(Modifiers); always set DTO.Modifiers = [] (or omit).
- **AdminProductsController:** Use only MapToModifierGroupDtoForAdminProduct (Modifiers = []) or equivalent.
- **ProductController:** Remove or narrow MapToModifierGroupDto that uses g.Modifiers if still referenced; keep MapToModifierGroupDtoForPos.
- **DTOs:** ModifierGroupDto.Modifiers — keep property for contract, always empty; or mark deprecated and eventually remove with API versioning.
- **POS productModifiersService:** Keep ModifierDto/ModifierGroupDto.modifiers for type shape (can be empty); mapGroupForPOS already sets modifiers: [].
- **Phase2DtoCompatibilityTests, Phase2ModifierGroupProductsTests, CatalogStructureTests:** Update or remove assertions for Modifiers.
- **Phase2ReceiptFlatTests, Phase2TableOrderRecoveryTests, Phase2CartFlatAddOnTests:** Keep tests that assert legacy read path (cart/table order/receipt with modifiers); remove or adjust ProductModifier seeding when table is dropped.

### High-risk (do not remove until legacy data is migrated or strategy is fixed)

- **CartController:** `.ThenInclude(i => i.Modifiers)` and building SelectedModifiers from CartItem.Modifiers / TableOrderItem.Modifiers. **Required for runtime** for any cart or table order that still has CartItemModifier/TableOrderItemModifier rows. Removing breaks loading and recovery.
- **PaymentService:** Read path for payment items with Modifiers (1162–1190) for receipt/snapshot. **Required for correct receipts and VAT** for existing payments that have item.Modifiers.
- **ReceiptService:** hasLegacyModifiers and line/tax math (126–147). **Required for receipt generation** for legacy payments.
- **TableOrderService:** Include(Modifiers) for table order items. **Required for recovery** of table orders with modifiers.
- **POS CartContext, CartItemRow, CartDisplay, ReceiptSummary, ExtrasChips, cash-register, _layout:** Reading and displaying `item.modifiers` / SelectedModifiers. **Required** so existing carts/orders with modifiers still display and total correctly. Do not remove until no such data exists or strategy is defined (e.g. server always returns empty and client tolerates it).

---

## Exact file list to change first (recommended order)

1. **frontend-admin/src/lib/api/legacyModifierMigration.ts** — Delete file.
2. **frontend-admin/src/app/(protected)/modifier-groups/page.tsx** — Remove: import and usage of getMigrationProgress, runBulkMigration, BulkMigrationResultDto, migrationProgressKey; state and handlers for bulk (bulkModalOpen, bulkLoading, bulkResult, bulkConfirm, bulkForm, handleBulkMigration, closeBulkModal); progress card (Statistic + Bulk button); bulk modal; single migrate modal and migrateLegacyModifier call; "Legacy-Modifier (Kompatibilität)" section and hint text. Keep: group CRUD, add product to group, group list.
3. **frontend-admin/src/lib/api/modifierGroups.ts** — Remove addLegacyModifierToGroup, migrateLegacyModifier, MigrateLegacyModifierBody, MigrateLegacyModifierResult; trim comments to remove legacy/migration wording. Keep getModifierGroups, addProductToGroup, etc.
4. **frontend-admin/src/features/products/components/ExtraZutatenSection.tsx** — Remove legacy modifier subsection (Modifier (Legacy, nur Leseansicht)) and any logic that displays modifiers from groups.
5. **backend/Controllers/AdminMigrationController.cs** — Delete controller or replace actions with 410 Gone.
6. **backend/Controllers/ModifierGroupsController.cs** — Remove Include(g => g.Modifiers); in MapToModifierGroupDto set Modifiers = []; remove or keep AddModifier (410); remove MigrateLegacyModifier action.
7. **backend/Program.cs** — Remove migrate-legacy-modifiers CLI block (args 240–274).
8. **backend/Services/ModifierMigrationService.cs** — Delete or gut (replace with 410/stub). Remove registration from DI.
9. **backend/Services/IModifierMigrationService.cs** — Delete. Remove from ModifierGroupsController and AdminMigrationController (if controller still exists).
10. **backend/DTOs/ModifierMigrationDTOs.cs** — Delete or move to deprecated folder.
11. **backend/Controllers/AdminProductsController.cs** — Use only MapToModifierGroupDtoForAdminProduct (Modifiers = []) for GetProductModifierGroups; remove MapToModifierGroupDto that fills Modifiers.
12. **backend/Controllers/ProductController.cs** — Ensure GetCatalog and GetProductModifierGroups never load or map Modifiers (already so); remove or narrow MapToModifierGroupDto if unused.
13. **backend/Services/ProductModifierValidationService.cs** — Remove or stub after no callers (PaymentService block is unreachable; ModifierMigrationService removed). Remove IProductModifierValidationService registration.
14. **backend/swagger.json** — Remove or mark deprecated: migration-progress, migrate-legacy-modifiers, modifiers/{id}/migrate-to-product, modifier-groups/{groupId}/modifiers, modifier-groups/{groupId}/modifiers/{modifierId}/migrate.
15. **backend/KasseAPI_Final.Tests/ModifierMigrationServiceTests.cs** — Delete or skip entire class.
16. **backend/KasseAPI_Final.Tests/ProductModifierValidationServiceTests.cs** — Delete when service removed.
17. **Backend:** Drop `product_modifiers` table (new EF migration) only after all reads/writes removed and backup/retention done. Remove ProductModifier entity and DbSet; remove Modifiers navigation from ProductModifierGroup.

Do **not** change first (high-risk): CartController Include(Modifiers) and SelectedModifiers build; PaymentService/ReceiptService legacy item.Modifiers read path; TableOrderService Include(Modifiers); POS CartContext and components that read item.modifiers — until legacy data is retired or strategy is set.

---

## Runtime risk points

- **Cart/table order load:** If `Include(i => i.Modifiers)` or mapping to SelectedModifiers is removed while CartItemModifier/TableOrderItemModifier rows exist, GET cart and table-order recovery can return incomplete data or fail; POS may show wrong totals or missing extras.
- **Receipt and VAT:** If PaymentService or ReceiptService no longer handles `item.Modifiers` for existing payment items, receipt line totals and VAT can be wrong for legacy payments; daily closing and reporting can be incorrect.
- **Admin modifier-groups:** Removing migration UI and API only affects admin workflow; no POS or fiscal impact if all legacy modifiers are already migrated (counts 0).

---

## Recommended removal order

1. **Phase 1 (admin-only, no runtime risk)**  
   - Remove migration progress and bulk migration (frontend-admin: legacyModifierMigration.ts, modifier-groups page progress/bulk/single migration UI and calls).  
   - Backend: AdminMigrationController (410 or delete), ModifierGroupsController.MigrateLegacyModifier and AddModifier (410), Program.cs CLI, ModifierMigrationService/interface, ModifierMigrationDTOs.  
   - Swagger: remove or deprecate migration and legacy-modifier routes.  
   - Tests: remove or skip ModifierMigrationServiceTests, ProductModifierValidationServiceTests when service removed.

2. **Phase 2 (admin + API response)**  
   - ModifierGroupsController and AdminProductsController: stop Include(Modifiers), always return Modifiers = [] (or omit).  
   - ExtraZutatenSection: remove legacy modifier subsection.  
   - modifierGroups.ts: remove migrateLegacyModifier and addLegacyModifierToGroup.  
   - DTOs: keep ModifierGroupDto.Modifiers for compatibility but always empty; document deprecated.  
   - Tests: Phase2DtoCompatibilityTests, Phase2ModifierGroupProductsTests, CatalogStructureTests — update for empty Modifiers.

3. **Phase 3 (DB and entity)**  
   - After confirming no code path reads/writes ProductModifiers: add migration to drop `product_modifiers`; remove ProductModifier entity, DbSet, and Modifiers navigation from ProductModifierGroup; remove ProductModifierValidationService and its registration.  
   - Backup product_modifiers if retention required.

4. **Phase 4 (legacy cart/order/receipt — only when safe)**  
   - When no (or negligible) cart/table order/payment data has embedded modifiers: consider stopping loading CartItemModifier/TableOrderItemModifier (or always return empty SelectedModifiers) and simplifying POS client to not depend on modifiers. This is **high-risk** and must be aligned with data retention and business sign-off.  
   - Until then: **do not** remove CartController/PaymentService/ReceiptService/TableOrderService read path for item.Modifiers or POS display of item.modifiers.
