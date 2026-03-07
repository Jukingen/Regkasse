# Phase D PR-A: POS-Only Cleanup (Legacy Modifier UI & Payload)

**Goal:** Safe POS-only cleanup after Phase C without changing add-on-as-product runtime behavior.

**Constraints respected:** No change to active add-on flow; no backend breaking changes; historical cart/payment compatibility paths kept; admin flows untouched.

---

## Touched files

| File | Change |
|------|--------|
| `frontend/components/PaymentModal.tsx` | Stopped setting `modifierIds` when building `paymentItems`; comment that Phase D does not emit modifierIds. |
| `frontend/services/api/paymentService.ts` | `PaymentItem` keeps optional `modifierIds?: string[]` for backend compat; comment that POS no longer sends it. |
| `frontend/app/(tabs)/cash-register.tsx` | Removed `handleAddModifier` and `onAddModifier` path; `usePOSOrderFlow` signature simplified; ProductList no longer receives `onAddModifier`. |
| `frontend/components/ProductList.tsx` | Removed `onAddModifier` from props and from ProductGridCard/ProductRow; `useInlineExtras` now `Boolean(onAddProduct)` only. |
| `frontend/components/ProductRow.tsx` | Removed `onAddModifier` from props and destructuring; dropped unused `ModifierOptionItem` import. |
| `frontend/components/ProductGridCard.tsx` | Removed `onAddModifier` from props and destructuring. |
| `frontend/components/ProductExtrasInline.tsx` | **Deleted.** |
| `frontend/components/ModifierSelectionModal.tsx` | **Deleted.** |

---

## Removed

- **Payment payload:** Generation and emission of `modifierIds` in the POS payment request (PaymentModal).
- **POS legacy modifier UI:**  
  - `handleAddModifier` and `onAddModifier` from cash-register, ProductList, ProductRow, ProductGridCard.  
  - Unused `ModifierOptionItem` import in ProductRow.
- **Components:**  
  - `ProductExtrasInline.tsx` (no imports; dead).  
  - `ModifierSelectionModal.tsx` (not mounted in any active POS flow; POS uses ModifierSelectionBottomSheet).

---

## Kept for compatibility

- **Backend / types:**  
  - `PaymentItem.modifierIds?: string[]` in `paymentService.ts` and related request typing so backend can still accept the field if sent elsewhere or in the future.  
  - No backend API or cart/payment contract changes.
- **Cart context:**  
  - `addModifier`, `incrementModifier`, `decrementModifier`, `removeModifier` and existing cart lines that use them (historical cart display/editing) are unchanged.
- **Admin / other apps:**  
  - No changes in admin or non-POS flows; no removal of shared code used by them.

---

## Follow-up for PR-B

- **Historical cart/payment:** If/when deprecating legacy modifier cart lines or payment payloads, consider removing or narrowing `modifierIds` in backend and types and cleaning CartContext modifier handlers.
- **AddItemToSpecificCart:** Still used by CartScreen only; candidate for Phase D PR-B or later when CartScreen is retired or switched to `add-item`.
- **Phase D notes:** Any component that was considered for removal but left in place due to risk should have a short “Phase D: …” comment; none were forced in PR-A.
