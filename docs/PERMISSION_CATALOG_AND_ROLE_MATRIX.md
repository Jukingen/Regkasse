# Permission Catalog & Role–Permission Matrix (Final)

**Source of truth:** `backend/Authorization/AppPermissions.cs`, `PermissionCatalog.cs`, `RolePermissionMatrix.cs`.

---

## 1. Final Permission Catalog

All permissions are in `resource.action` form. `PermissionCatalog.All` and policy registration use this list.

| Group | Permission | Constant |
|-------|------------|----------|
| **User & Role** | user.view | UserView |
| | user.manage | UserManage |
| | role.view | RoleView |
| | role.manage | RoleManage |
| **Product, Category, Modifier** | product.view | ProductView |
| | product.manage | ProductManage |
| | category.view | CategoryView |
| | category.manage | CategoryManage |
| | modifier.view | ModifierView |
| | modifier.manage | ModifierManage |
| **Order, Table, Cart, Sale** | order.view | OrderView |
| | order.create | OrderCreate |
| | order.update | OrderUpdate |
| | order.cancel | OrderCancel |
| | table.view | TableView |
| | table.manage | TableManage |
| | cart.view | CartView |
| | cart.manage | CartManage |
| | sale.view | SaleView |
| | sale.create | SaleCreate |
| | sale.cancel | SaleCancel |
| **Payment, Refund** | payment.view | PaymentView |
| | payment.take | PaymentTake |
| | payment.cancel | PaymentCancel |
| | refund.create | RefundCreate |
| | discount.apply | DiscountApply |
| **CashRegister, Cashdrawer, Shift** | cashregister.view | CashRegisterView |
| | cashregister.manage | CashRegisterManage |
| | cashdrawer.open | CashdrawerOpen |
| | cashdrawer.close | CashdrawerClose |
| | shift.view | ShiftView |
| | shift.open | ShiftOpen |
| | shift.close | ShiftClose |
| **Inventory, Customer** | inventory.view | InventoryView |
| | inventory.manage | InventoryManage |
| | inventory.adjust | InventoryAdjust |
| | inventory.delete | InventoryDelete |
| | customer.view | CustomerView |
| | customer.manage | CustomerManage |
| **Invoice, CreditNote** | invoice.view | InvoiceView |
| | invoice.manage | InvoiceManage |
| | invoice.export | InvoiceExport |
| | creditnote.create | CreditNoteCreate |
| **Settings, Localization, ReceiptTemplate** | settings.view | SettingsView |
| | settings.manage | SettingsManage |
| | localization.view | LocalizationView |
| | localization.manage | LocalizationManage |
| | receipttemplate.view | ReceiptTemplateView |
| | receipttemplate.manage | ReceiptTemplateManage |
| **Audit, Report** | audit.view | AuditView |
| | audit.export | AuditExport |
| | audit.cleanup | AuditCleanup |
| | report.view | ReportView |
| | report.export | ReportExport |
| **FinanzOnline** | finanzonline.view | FinanzOnlineView |
| | finanzonline.manage | FinanzOnlineManage |
| | finanzonline.submit | FinanzOnlineSubmit |
| **Kitchen** | kitchen.view | KitchenView |
| | kitchen.update | KitchenUpdate |
| **TSE** | tse.sign | TseSign |
| | tse.diagnostics | TseDiagnostics |
| **System-critical** | system.critical | SystemCritical |
| **Convenience** | price.override | PriceOverride |
| | receipt.reprint | ReceiptReprint |

**Total:** 57 permissions. No gaps; `PermissionCatalog.All` is built from `AppPermissions` and is the single list for policies and matrix.

---

## 2. Final Role–Permission Matrix Summary

| Role | Permissions (summary) |
|------|------------------------|
| **SuperAdmin** | All 57 (including system.critical, price.override). |
| **Admin** | All except **system.critical** (no permanent delete / system-critical ops). |
| **Manager** | UserView, RoleView; product/category/modifier view+manage; order/table/cart/sale full; payment (view/take/cancel), refund, discount, price override; cashregister.view, cashdrawer, shift; inventory view+manage; TseSign; customer view+manage; invoice view+manage+export; report view+export; audit view+export; settings.view; receipt reprint; kitchen view+update. **No:** UserManage, RoleManage, CashRegisterManage, InventoryDelete, AuditCleanup, SystemCritical. |
| **Cashier** | Product/category/modifier view; order view+create+update, table manage, cart view+manage; sale view+create; payment view+take+cancel, refund; discount, price override; cashregister view, cashdrawer, shift; inventory view; customer view+manage; invoice view; receipt reprint; kitchen view; TseSign. **No:** order.cancel, inventory manage/adjust/delete, invoice manage/export, report, audit, settings, user, role, catalog manage. |
| **Waiter** | Product/category/modifier view; order view+create+update+cancel, table manage; **cart.view only** (no cart.manage); sale view+create; payment view+take; shift view+close; customer view+manage; kitchen view. **No:** cart.manage, payment.cancel, refund, discount, price override, cashdrawer, shift open, inventory, invoice, TseSign, receipt reprint. |
| **Kitchen** | Order view+update; product view, category view; kitchen view+update. |
| **ReportViewer** | report.view, report.export, audit.view, settings.view. |
| **Accountant** | report.view, report.export, audit.view, finanzonline.view. |

---

## 3. Role-Based Capability Matrix

| Capability | SuperAdmin | Admin | Manager | Cashier | Waiter | Kitchen | ReportViewer | Accountant |
|------------|:----------:|:-----:|:-------:|:-------:|:------:|:-------:|:------------:|:----------:|
| **System / critical** | | | | | | | | |
| Permanent delete / system.critical | ✓ | — | — | — | — | — | — | — |
| User management (CRUD) | ✓ | ✓ | — | — | — | — | — | — |
| User list / view | ✓ | ✓ | ✓ | — | — | — | — | — |
| Role view | ✓ | ✓ | ✓ | — | — | — | — | — |
| Role manage | ✓ | ✓ | — | — | — | — | — | — |
| **Backoffice / catalog** | | | | | | | | |
| Product/category/modifier view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ (product, category) | — | — |
| Product/category/modifier manage | ✓ | ✓ | ✓ | — | — | — | — | — |
| **POS operations** | | | | | | | | |
| Order create/update/cancel | ✓ | ✓ | ✓ | ✓ (create/update) | ✓ | ✓ (update) | — | — |
| Table manage | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — |
| Cart view | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — |
| Cart manage | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| Sale create | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — |
| Payment take/cancel | ✓ | ✓ | ✓ | ✓ | ✓ (take) | — | — | — |
| Refund, discount, price override | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| **Register / shift** | | | | | | | | |
| Cash register view | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| Cash register manage | ✓ | ✓ | — | — | — | — | — | — |
| Cashdrawer / shift open–close | ✓ | ✓ | ✓ | ✓ | ✓ (shift close) | — | — | — |
| **Inventory** | | | | | | | | |
| Inventory view | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| Inventory manage | ✓ | ✓ | ✓ | — | — | — | — | — |
| Inventory delete | ✓ | — | — | — | — | — | — | — |
| **Customer** | | | | | | | | |
| Customer view/manage | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — |
| **Invoice / receipt** | | | | | | | | |
| Invoice view | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| Invoice manage/export | ✓ | ✓ | ✓ | — | — | — | — | — |
| Receipt reprint | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| **TSE** | | | | | | | | |
| TSE sign | ✓ | ✓ | ✓ | ✓ | — | — | — | — |
| TSE diagnostics | ✓ | ✓ | — | — | — | — | — | — |
| **Reports / audit** | | | | | | | | |
| Report view/export | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ |
| Audit view | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ |
| Audit export | ✓ | ✓ | ✓ | — | — | — | — | — |
| Audit cleanup | ✓ | ✓ | — | — | — | — | — | — |
| **Settings** | | | | | | | | |
| Settings view | ✓ | ✓ | ✓ | — | — | — | ✓ | — |
| Settings manage | ✓ | ✓ | — | — | — | — | — | — |
| **FinanzOnline** | | | | | | | | |
| FinanzOnline view | ✓ | ✓ | — | — | — | — | — | ✓ |
| FinanzOnline manage/submit | ✓ | ✓ | — | — | — | — | — | — |
| **Kitchen display** | | | | | | | | |
| Kitchen view/update | ✓ | ✓ | ✓ | ✓ (view) | ✓ (view) | ✓ | — | — |

**Legend:** ✓ = has permission; — = does not have permission.

---

## Design Notes

- **SuperAdmin vs Admin:** Only SuperAdmin has `system.critical` (e.g. permanent delete). Admin has all other permissions.
- **Manager:** Full operational and backoffice manage (catalog, inventory, reports, audit view/export), no user/role manage, no cash register manage, no inventory delete, no audit cleanup.
- **Waiter:** Order/table/sale/payment at table; cart.view only (no cart.manage). SaleCreate and PaymentTake kept for table-side payment flows.
- **ReportViewer:** Reports, audit view, settings view only.
- **Accountant:** Reports, audit view, FinanzOnline view only.
- **PermissionCatalog.All** and `RolePermissionMatrix` are kept in sync with `AppPermissions`; new permissions must be added in all three places.
