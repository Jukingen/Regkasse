# Add-on Product Validation Checklist

**Purpose:** Ensure add-on products behave like real products in all cart, pricing, receipt, and order flows. Add-ons must appear as their own cart/order lines for correct fiscal and receipt behavior.

**Date:** 2025-03-07

---

## 1. Validated Flows

### 1.1 Cart Line Creation
| Check | Status | Notes |
|-------|--------|-------|
| Add-on added via `addItem(productId, 1, { productName, unitPrice })` | ✅ | No modifiers; flat cart line |
| Backend `AddItemToCart` with `IsSellableAddOn` product | ✅ | Creates one CartItem, zero CartItemModifiers |
| Same add-on product merges (qty increases) | ✅ | Merge by ProductId when no modifiers |
| BuildCartResponse returns ProductName for add-ons | ✅ | From Product lookup |

### 1.2 Quantity Behavior
| Check | Status | Notes |
|-------|--------|-------|
| increment/decrement by itemId | ✅ | CartDisplay uses itemId/clientId |
| updateItemQuantityByItemId | ✅ | Per-line quantity update |
| removeByItemId | ✅ | Removes specific line |

### 1.3 Pricing Calculation
| Check | Status | Notes |
|-------|--------|-------|
| `getCartLineTotal(item)` for add-on | ✅ | `unitPrice * qty` (no modifier sum) |
| PaymentModal totalPrice fallback | ✅ | Uses `getCartLineTotal(item)` |
| Backend uses `product.Price` from DB | ✅ | Never trusts FE price |
| CartMoneyHelper for tax/totals | ✅ | Product.CategoryNavigation.VatRate |

### 1.4 Tax Handling
| Check | Status | Notes |
|-------|--------|-------|
| Add-on product has CategoryId | ✅ | Required for Product; tax from Category |
| PaymentService uses product.CategoryNavigation.VatRate | ✅ | Include(p => p.CategoryNavigation) |
| Receipt tax breakdown by TaxType/TaxRate | ✅ | GroupBy items |

### 1.5 Receipt Rendering
| Check | Status | Notes |
|-------|--------|-------|
| One ReceiptItemDTO per PaymentItem | ✅ | Flat path (no Modifiers) |
| Add-on appears as own line | ✅ | Phase2ReceiptFlatTests |
| IsModifierLine = false for add-ons | ✅ | Product-only lines |
| Price/tax totals correct | ✅ | CreatePayment_WithBaseProductAndAddOn_PriceAndTaxTotalsCorrect |

### 1.6 Order Persistence
| Check | Status | Notes |
|-------|--------|-------|
| TableOrderService: one TableOrderItem per cart item | ✅ | cart.Items.Zip → tableOrderItems |
| No new TableOrderItemModifier writes | ✅ | Phase 3 prep |
| Cart → TableOrder totals include all lines | ✅ | itemLineTotals from cart |

### 1.7 Fiscal / Accounting
| Check | Status | Notes |
|-------|--------|-------|
| TSE signature on receipt | ✅ | Per payment (not per line) |
| FinanzOnline mapping | ✅ | Invoice from Payment (do-not-touch) |
| Receipt numbering | ✅ | Per receipt (do-not-touch) |

---

## 2. Legacy Compatibility

### 2.1 Kept for Read-Only / Historical
- **CartItemModifier** – Existing carts with embedded modifiers still load; no new writes for add-ons.
- **TableOrderItemModifier** – Historical table orders; new conversions create flat TableOrderItems only.
- **PaymentItem.Modifiers** – Legacy payment snapshots; new payments are product-only (flat).
- **ReceiptItem.ParentItemId / IsModifierLine** – Legacy receipts with nested modifier lines; new receipts are flat.

### 2.2 Deprecated but Accepted
- **AddItemToCartRequest.SelectedModifiers** – Accepted for backward compat; ignored for write (Phase 3).
- **PaymentItemRequest.ModifierIds / Modifiers** – Accepted; ignored for write.

### 2.3 Observability Logs
When legacy paths are used, logs appear:
- `Phase2.LegacyModifier.AddItemRequestSelectedModifiers`
- `Phase2.LegacyModifier.CartLoadedWithEmbeddedModifiers`
- `Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers`
- `Phase2.LegacyModifier.ReceiptRenderedFromLegacyModifierSnapshots`

---

## 3. Tests

### Backend
- `Phase2CartFlatAddOnTests.AddItem_WithSellableAddOnProduct_DoesNotCreateCartItemModifiers`
- `Phase2ReceiptFlatTests.GetReceiptData_FromFlatPayment_ReturnsOneLinePerItemNoModifierLines`
- `Phase2ReceiptFlatTests.CreatePayment_WithBaseProductAndAddOn_PriceAndTaxTotalsCorrect`
- `Phase2TableOrderRecoveryTests.GetTableOrdersForRecovery_WithLegacyTableOrderItemModifiers_SerializesSelectedModifiers`

### Frontend
- `addOnFlow.test.ts` – groupsWithProducts, groupsWithModifiersOnly, hasModifiers, getCartLineTotal logic

---

## 4. Remaining Risks

| Risk | Mitigation |
|------|------------|
| Add-on product without CategoryId | Product.CategoryId required; admin must set when creating add-on |
| grandTotalGross from backend | FE uses backend totals; no local recalculation |
| Multiple payment flows (invoices, etc.) | Ensure all use same cart→payment mapping (one PaymentItem per cart line) |

---

## 5. File Reference

| Area | Path |
|------|------|
| Cart add-item | `backend/Controllers/CartController.cs` |
| Cart totals | `backend/Services/CartMoneyHelper.cs` |
| Table order | `backend/Services/TableOrderService.cs` |
| Payment | `backend/Services/PaymentService.cs` |
| Receipt | `backend/Services/PaymentService.GetReceiptDataAsync` |
| FE cart | `frontend/contexts/CartContext.tsx` |
| FE payment modal | `frontend/components/PaymentModal.tsx` |
| FE layout (cartItems) | `frontend/app/(tabs)/_layout.tsx` |
