# Endpoint Bazlı Permission Matrisi

**Tarih:** 2025-03-09

**Bağlam:** POS + backoffice hibrit; authorization şu an role bazlı, hedef role + permission; Administrator legacy alias (hedef rol Admin); minimum kırılımlı migration; backoffice ile operasyonel POS ayrımı; legacy api/Payment deprecated, yeni api/pos/payment; kritik alanlar: payment, refund, order status/cancel, company settings, audit cleanup, finanzonline config/submit, inventory adjust. Tasarım: rol = iş profili, permission = aksiyon yetkisi, scope = tenant/branch/ownership.

**Kurallar:** Read → *.view; Create → *.create; Update/state-change → *.update; Delete/cancel/destructive → *.cancel veya *.manage. Finansal: payment.take, payment.cancel, refund.create, sale.create, sale.cancel, invoice.view, invoice.manage, invoice.export, creditnote.create, finanzonline.view, finanzonline.manage, finanzonline.submit. Ayar/denetim: settings.view, settings.manage, localization.manage, receipttemplate.manage, audit.view, audit.export, audit.cleanup. Envanter: inventory.view, inventory.manage, inventory.adjust. Sipariş/POS: order.view, order.create, order.update, order.cancel, table.view, table.manage, cart.manage.

**Roller:** SuperAdmin, Admin, Manager, Cashier, Waiter; opsiyonel: Kitchen, ReportViewer, Accountant.

**Risk:** Low | Medium | High | Critical.

---

## 1) POS Payment

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/pos/payment, api/Payment | GET (methods, {id}, receipt, statistics, date-range, customer/{id}, method/{method}, {id}/qr) | payment.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Ödeme/metot/istatistik okuma |
| api/pos/payment, api/Payment | POST (create) | payment.take | SuperAdmin, Admin, Manager, Cashier | Medium | Ödeme alma – finansal |
| api/pos/payment, api/Payment | POST {id}/tse-signature | payment.take | SuperAdmin, Admin, Manager, Cashier | Medium | TSE imza – fiscal |
| api/pos/payment, api/Payment | POST {id}/cancel | payment.cancel | SuperAdmin, Admin, Manager, Cashier | **Critical** | Ödeme iptali – finansal |
| api/pos/payment, api/Payment | POST {id}/refund | refund.create | SuperAdmin, Admin, Manager, Cashier | **Critical** | İade – finansal |
| api/pos/payment, api/Payment | GET {id}/signature-debug | settings.manage | SuperAdmin, Admin | High | TSE tanılama – sistem |
| api/pos/payment, api/Payment | POST verify-signature | settings.manage | SuperAdmin, Admin | High | TSE doğrulama – sistem |

---

## 2) Orders – status / cancel

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/Orders | GET (list, {id}, status/{status}) | order.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Sipariş listesi/detay/durum okuma |
| api/Orders | POST (create) | order.create | SuperAdmin, Admin, Manager, Cashier, Waiter | Medium | Sipariş oluşturma |
| api/Orders | PUT {id}/status | order.update | SuperAdmin, Admin, Manager, Cashier, Waiter | **High** | Durum değişikliği (iptal vb.) |
| api/Orders | DELETE {id} | order.cancel | SuperAdmin, Admin, Manager, Cashier, Waiter | **High** | Sipariş iptal/silme |

---

## 3) Admin catalog

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/admin/categories | GET (list, {id}, {id}/products, search) | category.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Kategori okuma |
| api/admin/categories | POST, PUT {id}, DELETE {id} | category.manage | SuperAdmin, Admin, Manager | Medium | Kategori CRUD |
| api/admin/products | GET (list, {id}, search, {id}/modifier-groups) | product.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Ürün okuma |
| api/admin/products | POST, PUT {id}, PUT stock/{id}, DELETE {id}, POST {id}/modifier-groups | product.manage | SuperAdmin, Admin, Manager | Medium | Ürün CRUD |
| api/admin/users | GET (list, {id}, {id}/activity) | user.view | SuperAdmin, Admin, Manager, ReportViewer | Low | Kullanıcı listesi/detay |
| api/admin/users | POST, PATCH {id}, deactivate, reactivate, force-password-reset | user.manage | SuperAdmin, Admin | High | Kullanıcı yaşam döngüsü |
| api/modifier-groups | GET (list, {id}) | product.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Modifier okuma |
| api/modifier-groups | POST, PUT {id}, DELETE {id}, POST {id}/products, DELETE products/{productId} | product.manage | SuperAdmin, Admin, Manager | Medium | Modifier yazma |
| api/Product, api/pos | GET (list, all, catalog, active, categories, category/{name}, search, {id}/modifier-groups) | product.view | (aynı) | Low | Ürün okuma (POS) |
| api/Product, api/pos | POST, PUT {id}, PUT stock/{id}, POST {id}/modifier-groups | product.manage | SuperAdmin, Admin, Manager | Medium | Ürün yazma |

---

## 4) Inventory

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/Inventory | GET (list, {id}, low-stock, transactions/{id}) | inventory.view | SuperAdmin, Admin, Manager, Cashier | Low | Stok okuma |
| api/Inventory | POST (create), PUT {id}, POST {id}/restock | inventory.manage | SuperAdmin, Admin, Manager | Medium | Stok oluşturma/güncelleme/restock |
| api/Inventory | POST {id}/adjust | inventory.adjust | SuperAdmin, Admin, Manager | **High** | Stok miktar düzeltme – kritik alan |
| api/Inventory | DELETE {id} | inventory.manage | SuperAdmin, Admin | High | Stok silme |

---

## 5) CompanySettings / Localization

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/CompanySettings | GET (main, business-hours, banking, localization, billing, export) | settings.view | SuperAdmin, Admin, Manager | Low | Ayarlar okuma |
| api/CompanySettings | PUT (main, business-hours, banking, localization, billing) | settings.manage | SuperAdmin, Admin | **Critical** | Şirket ayarı – kritik |
| api/CompanySettings | GET export | settings.manage | SuperAdmin, Admin | High | Ayarlar dışa aktarma |
| api/Localization | GET (main, languages, currencies, timezones, format, currency) | settings.view | SuperAdmin, Admin, Manager | Low | Yerelleştirme okuma |
| api/Localization | PUT, POST add-language, add-currency, DELETE remove-* | localization.manage | SuperAdmin, Admin | High | Yerelleştirme yazma |
| api/Localization | GET export | localization.manage | SuperAdmin, Admin | Medium | Export |
| api/Settings | GET (main, tax-rates, backup, notifications) | settings.view | SuperAdmin, Admin | Low | Sistem ayarları okuma |
| api/Settings | PUT, PUT tax-rates, POST backup/now, PUT notifications | settings.manage | SuperAdmin, Admin | High | Sistem ayarları yazma |
| api/MultilingualReceipt | GET (list, {id}) | receipttemplate.view | SuperAdmin, Admin, Manager | Low | Fiş şablonu okuma |
| api/MultilingualReceipt | POST, PUT {id}, DELETE {id}, GET export | receipttemplate.manage | SuperAdmin, Admin | High | Fiş şablonu yazma |

---

## 6) AuditLog

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/AuditLog | GET (list, {id}, user/{id}, correlation/{id}, transaction/{id}, statistics, suspicious-admin-actions) | audit.view | SuperAdmin, Admin, Manager, ReportViewer, Accountant | Low | Denetim listesi/detay okuma |
| api/AuditLog | GET payment/{paymentId} | audit.view | SuperAdmin, Admin, Manager, Cashier | Low | Ödeme denetimi (Cashier kendi kaydı) |
| api/AuditLog | GET export | audit.export | SuperAdmin, Admin, Manager, ReportViewer, Accountant | High | Denetim dışa aktarma |
| api/AuditLog | DELETE cleanup | audit.cleanup | SuperAdmin, Admin | **Critical** | Denetim verisi silme – destructive |

---

## 7) Invoice / FinanzOnline

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/Invoice | GET (list, pos-list, {id}, search, status/{status}, {id}/pdf) | invoice.view | SuperAdmin, Admin, Manager, Cashier, ReportViewer, Accountant | Low | Fatura listesi/detay/PDF |
| api/Invoice | GET export | invoice.export | SuperAdmin, Admin, Manager, ReportViewer, Accountant | Medium | Fatura dışa aktarma |
| api/Invoice | POST, PUT {id}, DELETE {id}, POST {id}/duplicate | invoice.manage | SuperAdmin, Admin, Manager | High | Fatura CRUD |
| api/Invoice | POST {id}/credit-note | creditnote.create | SuperAdmin, Admin, Manager | **Critical** | Kredi notu – finansal |
| api/Invoice | POST backfill-from-payments | settings.manage | SuperAdmin, Admin | **Critical** | Toplu backfill – sistem |
| api/FinanzOnline | GET (config, status, errors, history/{id}) | finanzonline.view | SuperAdmin, Admin | Low | Config/status okuma |
| api/FinanzOnline | PUT config | finanzonline.manage | SuperAdmin, Admin | **Critical** | FinanzOnline konfigürasyonu |
| api/FinanzOnline | POST submit-invoice | finanzonline.submit | SuperAdmin, Admin | **Critical** | Fatura gönderimi – finansal |
| api/FinanzOnline | POST test-connection | finanzonline.manage | SuperAdmin, Admin | Medium | Bağlantı testi |

---

## 8) Diğer (Cart, Table, CashRegister, Reports, Receipts, TSE, Tagesabschluss, UserManagement, EntityController, Auth)

| Route | Method | SuggestedPermission | SuggestedRoles | Risk | Rationale |
|-------|--------|---------------------|----------------|------|------------|
| api/pos/cart, api/Cart | GET (current, {cartId}, history, table-orders-recovery) | sale.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Sepet okuma |
| api/pos/cart, api/Cart | POST, add-item, {cartId}/items, clear, complete, reset-after-payment, increment/decrement; PUT items/*; DELETE * | cart.manage | SuperAdmin, Admin, Manager, Cashier, Waiter | Medium | Sepet yazma / tamamlama (sale.create alternatif: cart.manage) |
| api/pos/cart, api/Cart | POST force-cleanup | cart.manage veya settings.manage | SuperAdmin, Admin, Cashier | High | Manuel sepet temizliği |
| api/Table | GET (list, {id}) | table.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Masa okuma |
| api/Table | POST {id}/status | table.manage | (aynı) | Medium | Masa durumu |
| api/CashRegister | GET (list, {id}, {id}/transactions) | cashregister.view | SuperAdmin, Admin, Manager, Cashier | Low | Kasa okuma |
| api/CashRegister | POST (create) | settings.manage | SuperAdmin, Admin | High | Kasa kaydı oluşturma |
| api/CashRegister | POST {id}/open | cashdrawer.open | SuperAdmin, Admin, Manager, Cashier | Medium | Kasa çekmecesi açma |
| api/CashRegister | POST {id}/close | shift.close | SuperAdmin, Admin, Manager, Cashier | High | Vardiya kapatma |
| api/Reports | GET (sales, products, customers, inventory, payments) | report.view | SuperAdmin, Admin, Manager, ReportViewer, Accountant | Low | Rapor okuma |
| api/Reports | GET export/sales | report.export | (aynı) | Medium | Rapor dışa aktarma |
| api/Receipts | GET {receiptId}, GET {receiptId}/signature-debug | sale.view | SuperAdmin, Admin, Manager, Cashier, Waiter | Low | Fiş okuma |
| api/Receipts | POST create-from-payment/{paymentId} | payment.take | SuperAdmin, Admin, Manager, Cashier | Medium | Fiş oluşturma |
| api/Tse | GET status, GET devices | finanzonline.view veya shift.view | SuperAdmin, Admin, Manager, Cashier | Low | TSE durumu |
| api/Tse | POST connect, signature, disconnect | payment.take | (aynı) | Medium | TSE işlemleri |
| api/Tagesabschluss | GET (history, can-close/{id}, statistics) | shift.view | (aynı) | Low | Vardiya okuma |
| api/Tagesabschluss | POST daily, monthly, yearly | shift.close | (aynı) | High | Vardiya kapatma |
| api/Customer | GET *, POST, PUT {id} | customer.view / customer.manage | (table.view rolleri) | Low/Medium | Müşteri CRUD |
| api/UserManagement | GET, GET {id}, GET roles | user.view | SuperAdmin, Admin, Manager, ReportViewer | Low | Kullanıcı okuma |
| api/UserManagement | POST, PUT {id}, deactivate, reactivate, reset-password, DELETE, POST roles | user.manage | SuperAdmin, Admin | High | Kullanıcı yönetimi |
| api/{entity} (EntityController) | GET, GET {id}, POST, PUT {id}, DELETE {id} | resource.view / resource.manage | (controller’a göre) | Low–High | Genel CRUD |
| api/{entity}/{id}/permanent | DELETE | settings.manage | SuperAdmin, Admin | **Critical** | Kalıcı silme |
| api/Auth (login, register, refresh, me) | POST/GET | (yok – public veya authenticated) | — | Low | Kimlik doğrulama |

---

## 9) İlk sprintte permission’a geçirilecek 10 endpoint

Aşağıdaki 10 endpoint, kritik alanlar ve yüksek risk nedeniyle ilk sprintte permission policy’ye taşınacak adaylardır.

| # | Route | Method | SuggestedPermission | Risk | Gerekçe |
|---|-------|--------|---------------------|------|--------|
| 1 | api/pos/payment, api/Payment | POST {id}/cancel | payment.cancel | Critical | Ödeme iptali – finansal |
| 2 | api/pos/payment, api/Payment | POST {id}/refund | refund.create | Critical | İade – finansal |
| 3 | api/Orders | PUT {id}/status | order.update | High | Sipariş durum değişikliği |
| 4 | api/Orders | DELETE {id} | order.cancel | High | Sipariş iptal |
| 5 | api/CompanySettings | PUT (main ve alt uçlar) | settings.manage | Critical | Şirket ayarı güncelleme |
| 6 | api/AuditLog | DELETE cleanup | audit.cleanup | Critical | Denetim verisi silme |
| 7 | api/AuditLog | GET export | audit.export | High | Denetim dışa aktarma |
| 8 | api/FinanzOnline | PUT config | finanzonline.manage | Critical | FinanzOnline konfigürasyonu |
| 9 | api/FinanzOnline | POST submit-invoice | finanzonline.submit | Critical | Fatura gönderimi |
| 10 | api/Invoice | POST backfill-from-payments | settings.manage | Critical | Toplu backfill |

**Alternatif / ek (11–13):** api/pos/payment GET signature-debug & POST verify-signature (settings.manage), api/Inventory POST {id}/adjust (inventory.adjust), api/{entity}/{id}/permanent DELETE (settings.manage). İlk sprint 10 ile sınırlı kalmak istenirse yukarıdaki 10 yeterli; ek 3’ü ikinci sprint’e bırakılabilir.
