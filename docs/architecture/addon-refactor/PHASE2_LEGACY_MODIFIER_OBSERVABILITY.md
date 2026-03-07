# Phase 2 – Legacy Modifier Observability

Lightweight structured logging to measure remaining legacy modifier usage before Phase 3 removal. Behavior is unchanged; logs are observability-only.

---

## 1. Event names and where they are emitted

| Event name (message prefix) | Path | When it fires |
|----------------------------|------|----------------|
| **Phase2.LegacyModifier.AddItemRequestSelectedModifiers** | Cart add-item (legacy path) | Request included `SelectedModifiers` with at least one non-empty modifier. |
| **Phase2.LegacyModifier.UpdateItemRequestSelectedModifiers** | Cart update-item | Request included `SelectedModifiers` with at least one non-empty modifier. |
| **Phase2.LegacyModifier.CartLoadedWithEmbeddedModifiers** | BuildCartResponse | Cart had at least one item with embedded `CartItemModifier` (returned as `SelectedModifiers`). |
| **Phase2.LegacyModifier.PaymentCreatedWithLegacyModifierPayload** | PaymentService.CreatePaymentAsync | At least one payment item was built from `ModifierIds`/`Modifiers` (legacy payload). |
| **Phase2.LegacyModifier.ReceiptRenderedFromLegacyModifierSnapshots** | PaymentService.GetReceiptDataAsync | Receipt DTO was built from a payment that had at least one `PaymentItem` with `Modifiers` (legacy snapshots). |
| **Phase2.LegacyModifier.ReceiptCreatedFromLegacyModifierSnapshots** | ReceiptService.CreateReceiptFromPaymentAsync | Receipt entity was created from a payment that had at least one item with `Modifiers`. |
| **Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers** | ModifierGroupsController GetAll / GetById | Response included at least one group with non-empty `Modifiers` (legacy list). |
| **Phase2.LegacyModifier.TableOrderCreatedWithLegacyModifiers** | TableOrderService.ConvertCartToTableOrderAsync | Cart → TableOrder conversion copied at least one CartItem.Modifier to TableOrderItemModifier. |
| **Phase2.LegacyModifier.TableOrderUpdatedWithLegacyModifiers** | TableOrderService.UpdateExistingTableOrderAsync | TableOrder update from cart copied at least one CartItem.Modifier to TableOrderItemModifier. |

---

## 2. Structured properties (for querying)

Each log uses a stable message template and named properties so log aggregators can filter and count:

- **AddItemRequestSelectedModifiers:** `CartId`, `ProductId`, `ModifierCount`
- **UpdateItemRequestSelectedModifiers:** `CartItemId`, `ModifierCount`
- **CartLoadedWithEmbeddedModifiers:** `CartId`, `ItemsWithModifiersCount`
- **PaymentCreatedWithLegacyModifierPayload:** `CustomerId`, `ItemsWithModifiersCount`
- **ReceiptRenderedFromLegacyModifierSnapshots:** `PaymentId`, `ItemsWithLegacyModifiersCount`
- **ReceiptCreatedFromLegacyModifierSnapshots:** `PaymentId`, `ReceiptId`, `ItemsWithLegacyModifiersCount`
- **ModifierGroupDtoReturnedWithModifiers:** GetAll: `GroupsWithModifiersCount`; GetById: `GroupId`, `ModifiersCount`
- **TableOrderCreatedWithLegacyModifiers:** `TableOrderId`, `CartId`, `CopiedModifiersCount`
- **TableOrderUpdatedWithLegacyModifiers:** `TableOrderId`, `CartId`, `CopiedModifiersCount`

---

## 3. How to tell when legacy usage reaches zero

- **Query:** Filter logs by message containing `Phase2.LegacyModifier` (or by a dedicated property if your logger adds one).
- **Zero usage:** When no log with any of the event names above appears over a chosen window (e.g. 7 days) in production, that path has no remaining legacy usage.
- **Per path:** You can check each event name separately to see which legacy paths are still used (e.g. cart write vs cart read vs payment vs receipt vs modifier groups).

Example (Azure Log Analytics / KQL):

```kql
AppTraces
| where Message startswith "Phase2.LegacyModifier"
| summarize Count = count() by bin(TimeGenerated, 1d), Event = extract(@"Phase2\.LegacyModifier\.(\w+)", 1, Message)
```

Example (grep / text logs):

```bash
grep "Phase2.LegacyModifier" /var/log/app/*.log | wc -l
```

When all of these event names have zero occurrences in the chosen window, legacy modifier usage has reached zero and Phase 3 removal can be planned.

---

## 4. Files changed

| File | Change |
|------|--------|
| **backend/Controllers/CartController.cs** | Log when add-item request has `SelectedModifiers`; when update-item request has `SelectedModifiers`; when `BuildCartResponse` builds a cart that has any item with embedded modifiers. |
| **backend/Services/PaymentService.cs** | Log when a payment is created with at least one item from legacy ModifierIds/Modifiers; when `GetReceiptDataAsync` builds receipt from payment items that have `Modifiers`. |
| **backend/Services/ReceiptService.cs** | Log when `CreateReceiptFromPaymentAsync` creates receipt from payment items that have `Modifiers`. |
| **backend/Controllers/ModifierGroupsController.cs** | Log when GetAll returns any group with `Modifiers.Count > 0`; when GetById returns a group with `Modifiers.Count > 0`. |
| **backend/Services/TableOrderService.cs** | Log when ConvertCartToTableOrderAsync copies any CartItem.Modifiers to TableOrderItemModifier; when UpdateExistingTableOrderAsync does the same. |

---

## 5. Comments in code

At each log site the comment reads:

- **Phase 2 observability:** when this log stops appearing, [description of what “legacy” means for this path] anymore.

So cleanup is measurable per path without changing behavior.
