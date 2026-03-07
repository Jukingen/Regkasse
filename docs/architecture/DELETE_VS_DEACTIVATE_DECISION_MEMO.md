# Silme mi, Deaktivasyon mu? — Karar Belgesi

**Bağlam:** Avusturya POS fiş (Registrierkasse) izlenebilirliği — RKSV, BAO, DSGVO.  
**Hedef kitle:** Ürün, destek ve uyum ekipleri.  
**Son güncelleme:** Bu belge teknik kararı özetler; nihai hukuki/vergi onayı Steuerberater ve hukuk danışmanı ile yapılmalıdır.

---

## 1. Decision memo: Delete vs Deactivate

### Özet karar

**Varsayılan: Kullanıcılar fiziksel olarak silinmez; yalnızca deaktive edilir.**  
Fiziksel (kalıcı) silme yalnızca tanımlı istisna senaryolarda, onaylı prosedür ve teknik kontrollerle mümkün olmalıdır.

### Seçenek karşılaştırması

| Kriter | Fiziksel silme (hard delete) | Deaktivasyon (soft delete) |
|--------|-----------------------------|----------------------------|
| **RKSV / Belegausgabepflicht** | Fişteki cashier referansı (CashierId) kaybolur veya “orphan” kalır; “kim fişi kesti” sorusu cevapsız kalabilir. | CashierId aynı kalır; kullanıcı kaydı okunabilir (isim, rol) veya “Deaktiviert” olarak raporlanabilir. |
| **BAO saklama** | İşlem kayıtları ile kullanıcı eşlemesi kopar; saklama yükümlülüğü riski. | Tüm referanslar korunur; 7 yıl+ saklama anlamlı kalır. |
| **DSGVO** | Veri silme talebi ile çelişebilir; ancak meşaat/kanuni saklama zorunluluğu öncelikli olabilir. | Veri minimizasyonu: giriş engellenir, geçmiş veri meşaat nedeniyle saklanır; denge korunur. |
| **Denetim / Audit** | “Bu kullanıcı silindi” sonrası audit log’daki UserId anlamsız kalabilir. | Audit log’daki UserId her zaman bir kayda karşılık gelir; actor/target tutarlı. |
| **Destek / Operasyon** | Eski fişlerde “Bilinmeyen kasiyer” gibi belirsizlik. | Eski fişlerde kasiyer adı veya “X (deaktiviert)” gösterilebilir. |
| **Yan etki** | Receipt, Payment, AuditLog, Order vb. foreign key / referans bütünlüğü; cascade veya null’lama gerekir. | Referanslar değişmez; sadece IsActive = false ve giriş engeli. |

### Pros / Cons

**Deaktivasyon (önerilen varsayılan)**

- **Artıları:** Fiş/fatura ve audit izi bozulmaz; RKSV/BAO ile uyumlu; reactivate mümkün; destek ve denetim kolay.
- **Eksileri:** Kullanıcı tablosu büyür; listelerde “aktif” filtresi gerekir; DSGVO silme talebi özel prosedür gerektirir.

**Fiziksel silme**

- **Artıları:** Tablo küçülür; “hiç var olmamış” gibi gösterilebilir (DSGVO “unkenntlich” talebi için özel senaryo).
- **Eksileri:** Fiş/ödeme/audit referansları kopar veya null’lanır; RKSV/BAO açısından risk; geri alınamaz; denetimde açıklama zorunluluğu.

---

## 2. Recommended policy: Ne zaman fiziksel silme (varsa) kabul edilir?

### Varsayılan kural

- **Normal iş akışı:** Sadece **deaktivasyon**. Silme API’si kullanıcıyı kalıcı silmez; mevcut implementasyonda DELETE bile soft-delete (deactivate) yapıyor.
- **Fiziksel silme:** Yalnızca aşağıdaki istisnalar için, **yazılı onay ve kayıt altına alınmış prosedür** ile düşünülebilir.

### Olası istisna senaryoları (onay ve prosedür şart)

1. **Yasal zorunluluk:** Mahkeme/vergi dairesi talimatı (belgeli).
2. **Test/seed verisi:** Sadece geliştirme/test ortamında, fiş/ödeme/audit ile ilişkisi olmayan test kullanıcıları (örn. UserSeedData temizliği).
3. **GDPR “right to erasure” (özel durum):** Yasal danışmanlık sonrası, saklama yükümlülüğü bitmiş ve “silme” kararı verilmişse; bu durumda bile fiş/ödeme/audit satırları silinmemeli, sadece kişisel veri anonimleştirilebilir (RKSV/BAO öncelikli).

### Teknik kural

- **AdminUsersController:** Kullanıcı için **DELETE endpoint’i yok**; kalıcı silme yapılmaz.
- **UserManagementController DELETE:** Mevcut davranış **soft-delete** (IsActive = false) olarak kalmalı; isim “delete” olsa bile fiziksel silme **yapılmamalı**.
- **UserManager.DeleteAsync(ApplicationUser):** Sadece seed/özel bakım script’lerinde, fiş/audit’ten bağımsız test verisi için kullanılmalı; normal uygulama akışında çağrılmamalı.

---

## 3. Mandatory safeguards for deactivation

Deaktivasyon aşağıdaki güvencelerle yapılmalıdır.

| # | Güvence | Açıklama |
|---|---------|----------|
| 1 | **Zorunlu neden (reason)** | Deaktivasyon isteği audit uyumu için nedensiz kabul edilmez. API: `reason` alanı zorunlu; boş ise 400. |
| 2 | **Audit kaydı** | Her deaktivasyonda: USER_DEACTIVATE, actor, target user id, reason, timestamp. Kalıcı; değiştirilmez/silinmez. |
| 3 | **Kim, ne zaman (ApplicationUser)** | DeactivatedAt, DeactivatedBy, DeactivationReason alanları set edilir; raporlama ve denetim için. |
| 4 | **Oturum iptali** | Deaktive edilen kullanıcının oturumları (JWT/refresh) iptal edilir; tekrar giriş yapamaz. |
| 5 | **Kendini deaktive etme yasağı** | Actor = target ise 400 BusinessRule; yalnızca başka bir admin deaktive edebilir. |
| 6 | **Zaten deaktif kontrolü** | IsActive zaten false ise tekrar deactivate 400 BusinessRule. |
| 7 | **Sadece yetkili roller** | Deactivate/Reactivate yalnızca Administrator (veya tanımlı admin politikası) ile. |

Mevcut sistemde: AdminUsersController ve UserManagementController’da bu kurallar uygulanıyor; DELETE endpoint’i soft-delete + audit ile sınırlı (reason “Legacy DELETE (no reason)” ile loglanıyor — tercihen UI’dan sadece PUT deactivate + reason kullanılmalı).

---

## 4. Impact on receipts, payments, audit logs, and reporting

### Receipts (Fişler)

- **Receipt.CashierId:** Deaktivasyon veya silme **asla** bu alanı değiştirmemeli veya null’lamamalı.
- **Davranış:** Eski fişlerde “Bu fişi kesen kasiyer” her zaman CashierId ile tanımlanır; kullanıcı deaktif olsa bile kayıt okunur (isim/rol gösterimi veya “Deaktiviert” etiketi).
- **Raporlama:** Kasiyer bazlı raporlar deaktif kullanıcıları da içerebilir; filtre “sadece aktif” ise ayrı parametre ile yapılır.

### Payments (Ödemeler)

- Ödeme kayıtları kullanıcıya doğrudan FK ile bağlıysa, bu referans **silinmemeli**. Deaktivasyon referansı etkilemez.
- Raporlama: Ödeme “hangi kullanıcı tarafından” sorusu deaktif kullanıcı için de cevaplanabilir (userId korunur).

### Audit logs

- **Append-only:** Audit log satırları güncellenmez veya silinmez.
- **User lifecycle:** USER_DEACTIVATE, USER_REACTIVATE, USER_CREATE, USER_UPDATE vb. kayıtlarında UserId (actor) ve target (EntityName / RequestData) korunur.
- **Sonuç:** “Kim, kimi, ne zaman, neden deaktive etti” her zaman denetlenebilir.

### Reporting

- **Liste ekranları:** Varsayılan “yalnızca aktif” olabilir; “tümü / deaktifler” filtresi ile deaktif kullanıcılar gösterilebilir.
- **Fiş/ödeme raporları:** CashierId / userId ile join’de deaktif kullanıcılar da gelir; etiket “(deaktiviert)” veya benzeri ile ayırt edilebilir.
- **Saklama süresi:** RKSV/BAO gereği fiş ve audit verileri (ve ilgili kullanıcı referansları) tanımlı süre (örn. 7 yıl) saklanır; deaktivasyon bu süreyi kısaltmaz.

---

## 5. Example SOP for support team (Deaktivasyon)

**Amaç:** Müşteri veya dahili talep ile bir kullanıcı hesabının güvenli ve izlenebilir şekilde devre dışı bırakılması.

### Adımlar

1. **Talebi doğrula**
   - Kim talep ediyor (yetkili kişi / HR / yönetici)?
   - Hangi kullanıcı (ad, e-posta, kullanıcı adı) deaktive edilecek?
   - Geçerli neden (işten ayrılma, rol değişikliği, güvenlik olayı, vb.) nedir?

2. **Yetki kontrolü**
   - İşlemi yapacak kişi Administrator (veya eşdeğer) rolüne sahip mi?
   - Deaktive edilecek kişi kendisi mi? (Evet ise işlem yapılmaz; başka bir admin yapmalı.)

3. **Sistemde deaktivasyon**
   - FE-Admin → Users → ilgili kullanıcıyı bul (filtre: aktif).
   - “Deaktivieren” (Deaktivasyon) butonuna tıkla.
   - **Zorunlu alan:** “Grund (für Audit erforderlich)” — mutlaka doldur (örn. “Ausscheiden zum 31.03.2025”, “Sicherheitsvorfall”).
   - Onayla; başarı mesajını kontrol et.

4. **Kontrol**
   - Kullanıcı listede “Inaktiv” olarak görünmeli.
   - İsteğe bağlı: Audit log’da ilgili kullanıcı için USER_DEACTIVATE kaydı ve girilen neden görülebilir.

5. **Dokümantasyon (şirket politikasına göre)**
   - Talep tarihi, talep eden, neden ve işlem tarihi dahili tickette veya prosedür formunda kayıt altına alınır.
   - Gerekirse HR/uyum ile paylaşılır.

### Yapılmaması gerekenler

- **Neden alanını boş bırakmak:** Sistem reddeder; audit uyumu için neden zorunludur.
- **Kullanıcıyı “silmek” (silme butonu varsa):** Bu sistemde kullanıcı fiziksel silinmez; silme = deaktivasyon. Tercihen sadece “Deaktivieren” + neden kullanılır.
- **Eski fişleri değiştirmek veya CashierId’yi temizlemek:** Yapılmaz; fişler ve kasiyer referansları aynen kalır.

### Reaktivasyon

- Talep gelirse: Users → ilgili kullanıcı (Inaktiv) → “Reaktivieren”. İsteğe bağlı neden girilebilir. Audit’te USER_REACTIVATE oluşur.

---

## 6. Özet tablo

| Konu | Karar |
|------|--------|
| Varsayılan işlem | Deaktivasyon (soft delete) |
| Fiziksel silme | Sadece tanımlı istisnalar + onaylı prosedür; normal API’de sunulmaz |
| Deaktivasyon güvenceleri | Zorunlu neden, audit, DeactivatedAt/By/Reason, oturum iptali, kendini deaktive etme yasağı |
| Fişler / ödemeler | CashierId ve kullanıcı referansları değiştirilmez veya silinmez |
| Audit log | Append-only; user lifecycle kayıtları silinmez/güncellenmez |
| Raporlama | Deaktif kullanıcılar referans ve raporlarda görülebilir; “(deaktiviert)” ile ayırt edilebilir |
| Destek SOP | Yukarıdaki adımlar: talep doğrula → yetki → deaktivasyon (neden zorunlu) → kontrol → dokümantasyon |

---

*Bu belge, RKSV/BAO/DSGVO bağlamında “Silme mi, deaktivasyon mu?” sorusuna net teknik ve operasyonel yanıt vermek için hazırlanmıştır. Yasal veya vergi son kararı için mutlaka Avusturya hukuk ve vergi danışmanı ile teyit edin.*
