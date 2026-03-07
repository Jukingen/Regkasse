# Cart, Payment, and Receipt Simplification

Technical description of the Phase 2 simplification: flat product-only lines for new add-ons, with legacy modifier structures kept for backward compatibility.

---

## 1. Why simplify

- **One line per product:** Cart, payment, and receipt use the same mental model: each product (base or add-on) is one line. No nested “base + modifiers” for new flows.
- **Less branching:** New code paths treat add-ons as normal products; legacy paths remain only for reading/writing existing modifier data.
- **Clear contracts:** DTOs put product/line fields first; modifier fields are deprecated and last. New clients use products only.

---

## 2. Target vs legacy behaviour (summary)

| Area | Target (new add-ons) | Legacy (still supported) |
|------|----------------------|---------------------------|
| **Cart** | One `CartItem` per product; add-on = separate line, no `CartItemModifier`. | Base product line + `CartItemModifier` rows; `SelectedModifiers` in request/response. |
| **Payment** | One `PaymentItem` per product; `Modifiers` empty. | Base product line + `PaymentItem.Modifiers` (snapshots); `ModifierIds`/`Modifiers` in request. |
| **Receipt** | One receipt line per product; no nesting. | Main line + nested “+ Extra” lines from `PaymentItem.Modifiers`. |

---

## 3. Cart

### Write path

- **Add-item (POST add-item)**  
  - **Product is sellable add-on (`IsSellableAddOn`):** Treated as a **flat line only**. No modifier validation; no `CartItemModifier` created. Merges with an existing cart line that has the same `ProductId` and no modifiers (same add-on = one line, quantity updated).  
  - **Product is base (not add-on):** **Legacy path** unchanged. `SelectedModifiers` in the request are still validated and persisted as `CartItemModifier` for backward compatibility.

- **Update-item (PUT items/{itemId})**  
  - Unchanged. If the request sends `SelectedModifiers`, legacy modifier update still runs. Preferred model: flat cart; add-ons as separate lines.

### Read path

- **GET cart:** Cart is loaded with `Include(Items).ThenInclude(Modifiers)`. Legacy lines still have modifiers. `BuildCartResponse` maps `CartItem.Modifiers` → `CartItemResponse.SelectedModifiers`. Totals include modifier amounts. Old carts do not crash.

### API

- **Request:** `AddItemToCartRequest.SelectedModifiers` and `UpdateCartItemRequest.SelectedModifiers` remain accepted (deprecated). Prefer flat lines (add-ons via separate add-item with add-on product ID).
- **Response:** `CartItemResponse.SelectedModifiers` still populated when present (legacy). New add-ons appear as additional `Items`, not inside `SelectedModifiers`.

### Key files

- `backend/Controllers/CartController.cs` – Add-item flat path for `IsSellableAddOn`; GET cart `Include(Modifiers)`; DTO deprecation.
- `backend/Models/CartItem.cs`, `CartItemModifier.cs` – Deprecation comments (read-only for compatibility).

---

## 4. Payment

### Write path (CreatePaymentAsync)

- **Product.IsSellableAddOn:** Treated as **product-only:** no modifier resolution, no `PaymentItem.Modifiers`, no stock deduction. One flat `PaymentItem` with product price/tax.
- **Product not add-on:** Unchanged. Stock check and decrement; if request has `ModifierIds`/`Modifiers`, legacy modifier validation and `PaymentItemModifierSnapshot` list still run.

### Stored payload (PaymentDetails.PaymentItems JSON)

- **New add-on sales:** One `PaymentItem` per line with `Modifiers` = [].
- **Legacy payments:** Still have `PaymentItem.Modifiers` (PaymentItemModifierSnapshot) where applicable; receipt and history reads unchanged.

### API

- **Request:** `PaymentItemRequest.ModifierIds` / `.Modifiers` still accepted (deprecated). New clients should send add-ons as separate items with only `productId` / `quantity` / `taxType`.
- **Tax and totals:** VAT from `product.CategoryNavigation.VatRate` per line. Add-on products use their own category. Totals still from same `paymentItems` list (product lines + legacy modifier snapshots); reconciliation logic unchanged.

### Key files

- `backend/Services/PaymentService.cs` – For `IsSellableAddOn`: skip stock and modifier block; for non–add-on: keep legacy ModifierIds/Modifiers handling.
- `backend/DTOs/PaymentDTOs.cs` – Deprecation on `PaymentItemRequest.ModifierIds`, `.Modifiers`, `PaymentItemModifierRequest`.
- `backend/Models/PaymentItem.cs` – Deprecation on `PaymentItem.Modifiers` and `PaymentItemModifierSnapshot`.

---

## 5. Receipt

### Formatting strategy

- **Flat (new):** One receipt line per product. Add-on product = normal line (name, quantity, unit price, line total). No parent/child nesting.
- **Legacy (unchanged):** Main line per product; nested “+ Extra” lines from `PaymentItem.Modifiers` with `ParentItemId` / `IsModifierLine`. Historical receipts unchanged.

### Implementation

- **Backend (GetReceiptDataAsync, CreateReceiptFromPaymentAsync):**  
  - **Flat path:** `item.Modifiers` null or empty → one receipt line with full totals.  
  - **Legacy path:** `item.Modifiers` non-empty → one main line (totals = product line minus modifier amounts) + one nested line per modifier with “+ ” prefix and `ParentItemId` / `IsModifierLine`.  
- **Totals and tax:** Still derived from the same payment items (product + legacy modifier snapshots); no change to reconciliation.

### Frontend

- **ReceiptSummary / ReceiptLineItem / ReceiptTemplate:** Each line is either standalone (flat) or legacy modifier line. Backend may send “+ Extra Cheese” for legacy; frontend avoids adding a second “+ ”.

### Key files

- `backend/Services/PaymentService.cs` – GetReceiptDataAsync: flat-first; legacy branch only when `item.Modifiers` non-empty.
- `backend/Services/ReceiptService.cs` – CreateReceiptFromPaymentAsync: same flat vs legacy split.
- `backend/DTOs/ReceiptDTO.cs` – Comments on `ParentItemId`, `IsModifierLine` (legacy nested lines).
- `frontend/components/ReceiptSummary.tsx`, `ReceiptLineItem.tsx`, `ReceiptTemplate.tsx` – Modifier name as-is to avoid “+ + …”.

---

## 6. Compatibility

- **Historical data:** All legacy structures (cart_item_modifiers, PaymentItem.Modifiers, nested receipt lines) continue to be read and displayed. No breaking change for existing carts, orders, or receipts.
- **New data:** New add-on sales use flat model only; no new `CartItemModifier` or `PaymentItem.Modifiers` for add-on products.
- **DTOs:** Legacy properties remain in request/response; marked `[Obsolete(..., false)]` and documented. No removal in Phase 2.

---

## 7. Future Phase 3 (cart / payment / receipt)

- **Cart:** Ignore `SelectedModifiers` on add-item and update-item; stop writing `CartItemModifier`. Later remove or always-empty `SelectedModifiers` from DTOs.
- **Payment:** Ignore `ModifierIds`/`Modifiers` on `PaymentItemRequest`; stop writing `PaymentItemModifierSnapshot` for new payments. Simplify receipt generation to assume no `item.Modifiers` when all clients send flat items.
- **Receipt:** Keep legacy read path for historical receipts until data retention policy allows; then optionally simplify to flat-only.

---

## 8. References

- [PHASE2_IMPLEMENTATION.md](./PHASE2_IMPLEMENTATION.md) – Phase 2 overview, rollout, and risks.
- [LEGACY_MODIFIER_MIGRATION.md](./LEGACY_MODIFIER_MIGRATION.md) – Modifier → product migration.
- `ai/PHASE2_CART_FLAT_ITEMS.md`, `ai/PHASE2_PAYMENT_FLAT_ITEMS.md`, `ai/PHASE2_RECEIPT_FLAT_LINES.md`, `ai/PHASE2_LEGACY_DTO_EXPOSURE.md` – Step-level implementation notes.
