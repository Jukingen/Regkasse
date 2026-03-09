# Nihai Role–Permission Matrix v1

**Tarih:** 2025-03-09

**Bağlam:** POS + backoffice hibrit; hedef mimari role + permission; Administrator kalıcı hedef rol değil, sadece legacy alias; Admin = business/backoffice ana yönetim rolü; minimum kırılımlı migration; backoffice ile operasyonel POS yetkileri net ayrım.

**Hedef roller:** SuperAdmin, Admin, Manager, Cashier, Waiter.  
**Opsiyonel roller:** Kitchen, ReportViewer, Accountant.

---

## 1) Permission sözlüğü (mantıklı başlıklara göre)

### 1.1 Kullanıcı ve rol
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| user.view       | Kullanıcı listesi/detay okuma     |
| user.manage     | Kullanıcı oluşturma/güncelleme/silme, deaktive, şifre sıfırlama |
| role.view       | Rol listesi okuma                 |
| role.manage     | Rol atama/düzenleme               |

### 1.2 Katalog (ürün, kategori, modifier)
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| product.view    | Ürün/katalog okuma                |
| product.manage  | Ürün CRUD, stok alanı, modifier grupları |
| category.view   | Kategori okuma                    |
| category.manage | Kategori CRUD                     |
| modifier.view   | Modifier grubu okuma              |
| modifier.manage | Modifier grubu CRUD               |

### 1.3 Sipariş, masa, sepet, satış
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| order.view      | Sipariş listesi/detay okuma       |
| order.create    | Sipariş oluşturma                 |
| order.update    | Sipariş durum güncelleme          |
| order.cancel    | Sipariş iptal/silme               |
| table.view      | Masa listesi/durum okuma          |
| table.manage    | Masa durumu değiştirme            |
| cart.view       | Sepet okuma                       |
| cart.manage     | Sepet oluşturma/güncelleme/temizleme, tamamlama |
| sale.view       | Satış/fiş okuma                   |
| sale.create     | Satış tamamlama (sepet → ödeme)   |
| sale.cancel     | Satış iptali (nadir)              |

### 1.4 Ödeme ve iade
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| payment.view    | Ödeme listesi/detay/istatistik okuma |
| payment.take    | Ödeme alma, TSE imza, fiş oluşturma |
| payment.cancel  | Ödeme iptali                      |
| refund.create   | İade oluşturma                    |

### 1.5 Kasa, vardiya, çekmece
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| cashregister.view   | Kasa kaydı/transaksiyon okuma |
| cashregister.manage | Kasa kaydı oluşturma (sistem)  |
| cashdrawer.open | Kasa çekmecesi açma               |
| shift.view      | Vardiya/TSE durumu okuma          |
| shift.open      | Vardiya açma                      |
| shift.close     | Vardiya kapatma, Tagesabschluss   |

### 1.6 Envanter ve müşteri
| Permission      | Açıklama                          |
|-----------------|-----------------------------------|
| inventory.view  | Stok listesi/detay okuma          |
| inventory.manage| Stok CRUD, restock                |
| inventory.adjust| Stok miktar düzeltme (kritik)     |
| customer.view   | Müşteri okuma                     |
| customer.manage | Müşteri CRUD                      |

### 1.7 Fatura ve kredi notu
| Permission       | Açıklama                          |
|------------------|-----------------------------------|
| invoice.view     | Fatura listesi/detay/PDF okuma    |
| invoice.manage   | Fatura CRUD, duplicate            |
| invoice.export   | Fatura dışa aktarma               |
| creditnote.create| Kredi notu oluşturma             |

### 1.8 Ayar ve yerelleştirme
| Permission           | Açıklama                          |
|----------------------|-----------------------------------|
| settings.view        | Şirket/sistem ayarları okuma      |
| settings.manage      | Şirket/sistem ayarları yazma (CompanySettings, Localization yazma, TSE tanılama vb.) |
| localization.view   | Yerelleştirme okuma (Settings altı) |
| localization.manage | Yerelleştirme dil/para birimi yazma |
| receipttemplate.view | Fiş şablonu okuma                 |
| receipttemplate.manage | Fiş şablonu CRUD                |

### 1.9 Denetim ve rapor
| Permission   | Açıklama                          |
|--------------|-----------------------------------|
| audit.view   | Denetim kaydı okuma                |
| audit.export | Denetim dışa aktarma              |
| audit.cleanup| Eski denetim kayıtlarını silme    |
| report.view  | Rapor ekranları okuma             |
| report.export| Rapor dışa aktarma                |

### 1.10 FinanzOnline
| Permission          | Açıklama                          |
|---------------------|-----------------------------------|
| finanzonline.view  | Config/status/hata geçmişi okuma  |
| finanzonline.manage| Config güncelleme, test bağlantı  |
| finanzonline.submit| Fatura gönderimi (submit-invoice) |

### 1.11 Mutfak ve operasyonel kolaylık
| Permission     | Açıklama                          |
|----------------|-----------------------------------|
| kitchen.view   | Mutfak ekranı okuma               |
| kitchen.update | Mutfak sipariş durumu güncelleme  |
| price.override | Fiyat manuel override (indirim/özel fiyat) |
| receipt.reprint| Fiş yeniden yazdırma              |

---

## 2) Role–Permission matrix v1

Her rol için varsayılan permission seti. Administrator ayrı satırda yok; legacy alias olarak Admin ile aynı set kullanılır (bkz. Bölüm 4).

### 2.1 SuperAdmin
- **Tüm permission’lar.** (PermissionCatalog.All ile aynı set.)
- Teknik/sistem seviyesi; tenant/şirket üstü yönetim.

### 2.2 Admin
- **Tüm permission’lar.** (SuperAdmin ile aynı set; ileride scope/tenant ile kısıtlanabilir.)
- İşletme/backoffice ana yönetim rolü; kullanıcı, rol, ayar, FinanzOnline, audit cleanup.

### 2.3 Manager
- **user.view**, **role.view**
- **product.view**, **product.manage** | **category.view**, **category.manage** | **modifier.view**, **modifier.manage**
- **order.view**, **order.create**, **order.update**, **order.cancel** | **table.view**, **table.manage** | **cart.view**, **cart.manage** | **sale.view**, **sale.create**
- **payment.view**, **payment.take**, **payment.cancel** | **refund.create**
- **cashregister.view**, **cashdrawer.open** | **shift.view**, **shift.open**, **shift.close**
- **inventory.view**, **inventory.manage** (inventory.adjust yok; düzeltme Admin/operasyon politikası ile)
- **customer.view**, **customer.manage**
- **invoice.view**, **invoice.manage**, **invoice.export** (creditnote.create yok; kredi notu Admin)
- **settings.view** (settings.manage yok)
- **localization.view** (localization.manage yok)
- **receipttemplate.view** (receipttemplate.manage yok)
- **audit.view**, **audit.export** (audit.cleanup yok)
- **report.view**, **report.export**
- **finanzonline.view** (finanzonline.manage, finanzonline.submit yok)
- **kitchen.view**, **kitchen.update**
- **price.override**, **receipt.reprint**

Özet: Operasyon yöneticisi; katalog, sipariş, ödeme, iade, vardiya, rapor, denetim export; user/role/settings.manage ve FinanzOnline yazma/cleanup yok.

### 2.4 Cashier
- **product.view**, **category.view**, **modifier.view**
- **order.view**, **order.create**, **order.update** | **table.view**, **table.manage** | **cart.view**, **cart.manage** | **sale.view**, **sale.create**
- **payment.view**, **payment.take**, **payment.cancel** | **refund.create**
- **cashregister.view**, **cashdrawer.open** | **shift.view**, **shift.open**, **shift.close**
- **inventory.view**
- **customer.view**, **customer.manage**
- **invoice.view**
- **kitchen.view**
- **price.override**, **receipt.reprint**

Özet: Kasa/ödeme odaklı; order.cancel opsiyonel (v1’de iptal Manager/Cashier’da; Waiter sadece durum güncellemesi). payment.cancel ve refund.create Cashier’da olacak.

### 2.5 Waiter
- **product.view**, **category.view**, **modifier.view**
- **order.view**, **order.create**, **order.update**, **order.cancel** | **table.view**, **table.manage** | **cart.view**, **cart.manage** | **sale.view**, **sale.create**
- **payment.view**, **payment.take** (payment.cancel ve refund.create yok)
- **shift.view**, **shift.close**
- **customer.view**, **customer.manage**
- **kitchen.view**

Özet: Sipariş/masa odaklı; ödeme alabilir, iptal/iade yapmaz; sipariş iptali (order.cancel) garson akışında gerekli.

### 2.6 Kitchen (opsiyonel)
- **order.view**, **order.update**
- **product.view**, **category.view**
- **kitchen.view**, **kitchen.update**

Özet: Sadece mutfak/servis akışı; sipariş durumu güncelleme.

### 2.7 ReportViewer (opsiyonel)
- **report.view**, **report.export**
- **audit.view**, **audit.export**
- **invoice.view**, **invoice.export**
- **payment.view**
- **settings.view**

Özet: Salt okuma + export; yazma yok.

### 2.8 Accountant (opsiyonel)
- ReportViewer ile aynı set: **report.view**, **report.export** | **audit.view**, **audit.export** | **invoice.view**, **invoice.export** | **payment.view** | **settings.view**

Özet: Muhasebe raporlama; yazma yok.

---

## 3) Karar notları (sorulara cevaplar)

| Soru | Karar | Gerekçe |
|------|--------|--------|
| **Manager refund yapabilir mi?** | Evet. | Operasyon yöneticisi günlük iade kararlarını verebilmeli; refund.create Manager’da. |
| **Cashier payment cancel yapabilir mi?** | Evet. | Kasa operasyonu; yanlış ödeme iptali günlük akışta gerekli; payment.cancel Cashier’da. |
| **Waiter order cancel yapabilir mi?** | Evet. | Sipariş iptali garson akışının parçası; order.cancel Waiter’da. payment.cancel/refund Waiter’da yok (kasa ile sınırlı). |
| **price override kimde olmalı?** | Manager + Cashier. | Operasyonel indirim/özel fiyat; Waiter’da yok (tutarlılık için sadece kasa tarafında). |
| **audit export kimde olmalı?** | Manager + ReportViewer + Accountant (ve Admin/SuperAdmin). | Denetim raporlama; Manager operasyon denetimi, ReportViewer/Accountant salt rapor; audit.cleanup sadece Admin/SuperAdmin. |
| **settings.manage kimde olmalı?** | Sadece SuperAdmin + Admin. | Şirket ayarı, FinanzOnline, TSE tanılama, sistem ayarları; Manager dahil diğer rollerde yok. |

### Ek kararlar
- **Administrator:** Kalıcı hedef rol değil; sadece legacy alias → Admin ile aynı permission seti (Bölüm 4).
- **inventory.adjust:** Sadece Admin (ve SuperAdmin). Manager inventory.manage ile restock/CRUD yapabilir; miktar “düzeltme” (adjust) daha kritik, tek merkezde.
- **creditnote.create:** Sadece Admin (ve SuperAdmin). Manager invoice.export’a kadar; kredi notu finansal karar.
- **receipttemplate.manage / localization.manage:** Sadece Admin (ve SuperAdmin). Manager sadece view.

---

## 4) Administrator → Admin legacy mapping

- **Administrator** rolü artık yeni atanmayacak; mevcut kullanıcılar için **legacy alias** olarak kalacak.
- Yetki tarafında **Administrator = Admin** ile aynı permission seti kullanılacak: `RolePermissionMatrix` ve `RoleCanonicalization` içinde Administrator → Admin eşlemesi yapılmış durumda.
- JWT ve API yanıtlarında rol olarak **Admin** (canonical) dönülebilir; UI’da “Administrator” gösterimi istenirse ayrı bir display name ile korunabilir.
- Hedef: Zamanla tüm Administrator atamaları Admin’e taşınacak; yeni rol adı **Admin**.

---

## 5) Önerilen v1 default rol seti özeti

| Rol | Odak | Öne çıkan yetkiler | Olmayan yetkiler (v1) |
|-----|------|---------------------|-------------------------|
| **SuperAdmin** | Sistem | Tümü | — |
| **Admin** | Backoffice / işletme | Tümü (user, role, settings, FinanzOnline, audit.cleanup, creditnote, inventory.adjust) | — |
| **Manager** | Operasyon | Katalog CRUD, sipariş/ödeme/iade, vardiya, rapor/audit export, price.override | user.manage, role.manage, settings.manage, audit.cleanup, finanzonline.* yazma, creditnote.create, inventory.adjust |
| **Cashier** | Kasa / ödeme | Ödeme alma/iptal, iade, sepet, sipariş durum, vardiya, price.override, receipt.reprint | Katalog yazma, user/settings, audit.cleanup, invoice.manage/export |
| **Waiter** | Sipariş / masa | Sipariş CRUD (create/update/cancel), masa, sepet, payment.take, kitchen | payment.cancel, refund.create, price.override, katalog yazma, vardiya open |
| **Kitchen** | Mutfak | order.view/update, kitchen.view/update | Ödeme, sepet, ayar |
| **ReportViewer** | Raporlama | report, audit, invoice, payment view + export | Tüm yazma |
| **Accountant** | Muhasebe | ReportViewer ile aynı | Tüm yazma |

**V1 uygulama notu:** `RolePermissionMatrix.cs` bu tabloya göre senkron: Waiter'a `OrderCancel` eklendi; Manager'da `RefundCreate` var, `SettingsManage`, `AuditCleanup`, `FinanzOnlineManage`/`Submit`, `CreditNoteCreate`, `InventoryAdjust` yok; `InventoryAdjust` sadece Admin/SuperAdmin'de.
