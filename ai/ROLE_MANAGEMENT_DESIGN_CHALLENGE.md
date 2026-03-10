# Role Management Tasarımı – Challenge & Değerlendirme

**Amaç:** Mevcut tasarımı kod yazmadan eleştirel değerlendirmek; güçlü yanlar, riskler, alternatifler ve karar önerisi.

---

## 1) Güçlü yanlar

- **Tek kaynak kuralı (system vs custom):** Sistem rolleri sadece `RolePermissionMatrix` (kod), custom roller sadece `AspNetRoleClaims`; çakışma yok, davranış deterministik.
- **Mevcut davranış korunuyor:** Matrix’e dokunulmuyor; mevcut sistem rolü kullanan kullanıcıların token ve yetki davranışı değişmiyor.
- **API sınırları net:** PUT/DELETE SuperAdmin-only; sistem rolü için 400, rol yok 404, atanmış kullanıcı 409; invalid key 400. Hata tipleri tutarlı.
- **Audit:** ROLE_PERMISSIONS_UPDATE ve ROLE_DELETE AuditLog’a yazılıyor; kim, ne zaman, hangi rol için izlenebilir.
- **Identity ile uyum:** AspNetRoleClaims Identity’nin standart yapısı; ek tablo/migration yok, mevcut RoleManager API kullanılıyor.
- **UI ile uyum:** Catalog’ta key, group, resource, action var; gruplu checklist ve preset için yeterli. FE’de catalog/menu alignment uyarısı ile sürük (drift) fark edilebiliyor.

---

## 2) Tasarım riskleri

### API contract tutarlılığı

- **Login response vs token:** Token `TokenClaimsService` + `IRolePermissionResolver` ile üretiliyor (matrix + role claims birleşik). Buna karşılık login response içindeki `user.permissions` hâlâ `RolePermissionMatrix.GetPermissionsForRoles(roles)` ile dolduruluyor. Custom rolü olan kullanıcıda token doğru, ama API’den dönen `permissions` listesi eksik kalır. FE `user.permissions` ile menü/buton açıyorsa custom rol izinleri yansımaz; yetki token’da var ama UI’da yok.
- **Öneri:** Login (ve varsa /me) response’taki `permissions` alanı da resolver’dan üretilmeli; token ile aynı kaynak.

### System vs custom role ayrımı

- **Güçlü:** `Roles.Canonical` ile isim bazlı “sistem rolü” tanımı net; custom = AspNetRoles’ta olup Canonical’da olmayan. Silme/düzenleme kuralları buna göre doğru.
- **Risk:** Sistem rol listesi kodda sabit. Yeni canonical rol eklemek için kod değişikliği gerekir; “sistem rolü”nü konfig veya DB’den okumak istenirse mevcut tasarım genişlemez.
- **İkinci risk:** Sadece isim ile ayrım yapılıyor; rolün “sistem mi custom mı” bilgisi AspNetRoles’ta tutulmuyor. İleride aynı isimle farklı tenant’ta hem sistem hem custom kullanımı olursa bu model yetersiz kalır (şu an tek tenant varsayımında sorun yok).

### RoleClaims persist yaklaşımının riskleri

- **Round-trip sayısı:** Her `AddClaimAsync` / `RemoveClaimAsync` ayrı DB çağrısı. 20 permission’lı bir set güncellemesi = mevcut N remove + 20 add; N+20 round-trip. Permission sayısı büyürse gecikme artar.
- **Eşzamanlı güncelleme:** Optimistic concurrency yok. İki SuperAdmin aynı custom rolü aynı anda düzenlerse son yazan kazanır; diğerinin değişiklikleri kaybolur.
- **Claim type tekliği:** Sadece `ClaimType = "permission"`, `Value = key`. İleride koşullu izin (örn. branch-scoped) veya metadata eklemek için ya yeni claim type’lar ya da Value’da yapı (örn. JSON) gerekir; mevcut yapı basit senaryo için uygun, genişlemesi sınırlı.
- **Veri bütünlüğü:** Rol silindiğinde Identity AspNetRoleClaims’i cascade ile silebilir (EF/DB’ye bağlı). Kullanıcı hâlâ o rolü taşıyorsa (AspNetUserRoles) token’da rol adı var ama resolver `FindByNameAsync` ile null döner; o rolden permission gelmez. Orphan davranışı kabul edilebilir ama “rol silindi, kullanıcı hâlâ eski rol adına sahip” durumu audit’te net değil.

### Token claim üretiminde edge case’ler

- **Çoklu rol birleşimi:** Kullanıcı hem sistem hem custom role sahipse resolver her iki kaynaktan permission’ları birleştiriyor; token’da union doğru. Davranış doğru.
- **Login response permissions:** Yukarıda belirtildiği gibi login cevabındaki `permissions` matrix’ten; custom rol izinleri response’ta yok. FE bu listeye güveniyorsa yanlış “yetkisiz” görünüm oluşur.
- **PermissionAuthorizationHandler fallback:** Token’da permission claim yoksa handler `RolePermissionMatrix.GetPermissionsForRoles(roles)` ile fallback yapıyor. Token’da claim varsa matrix’e hiç bakmıyor. Custom rol kullanıcısında token’da zaten resolver’dan gelen claim’ler olacağı için fallback nadiren devreye girer; tek risk, token’ın eski (permission’sız) üretildiği ve cache’lendiği bir senaryo. Genel olarak tutarlı.
- **Büyük/küçük harf:** Sistem rolü kontrolü `OrdinalIgnoreCase`; canonical isimler sabit. Identity’de rol adı genelde tek biçimde yazıldığı için pratikte sorun beklenmez.

### Permission catalog’un UI için yeterliliği

- **Yeterli olan:** Key, group, resource, action; gruplu liste ve preset için uygun. `PermissionCatalogMetadata.GetDescription(key)` şu an null; UI’da sadece key gösteriliyor.
- **Eksik:** Açıklama (description) yok; erişilebilirlik veya tooltip için zayıf. İsteğe bağlı: “Bu izin menü X’i açar” gibi kısa metin eklenebilir.
- **Catalog kaynağı:** Tüm izinler `PermissionCatalog.All` (kod). Yeni permission = deploy. Konfig veya tenant bazlı yeni izin tanımı bu tasarımla yok.

---

## 3) Alternatifler

### Login/me permissions tutarlılığı

- **A:** Login ve /me’de `permissions` alanını da `IRolePermissionResolver` ile doldur (token ile aynı kaynak). Küçük değişiklik, tutarlılık sağlar.
- **B:** FE’de hiç `user.permissions` kullanma; sadece token’daki claim’leri kullan. Backend’de login response’tan permissions’ı kaldır veya “informational” say. Token zaten doğru; FE’in claim’leri okuyabilmesi gerekir (çoğu durumda JWT decode ile alınır).

### RoleClaims persist

- **Toplu güncelleme:** Tek transaction’da tüm permission claim’leri silip yenilerini eklemek için RoleStore’da toplu API yok; Identity tek tek claim API sunuyor. Alternatif: geçici olarak custom bir “RolePermissions” tablosu (RoleId, PermissionKey) ve tek UPDATE/INSERT/DELETE ile senkronize etmek; ardından token tarafında bu tabloyu okuyacak bir resolver. Daha fazla mimari değişiklik; mevcut çözüm basit senaryoda kabul edilebilir, büyük set’lerde performans izlenmeli.
- **Concurrency:** Optimistic concurrency (row version) veya “last write wins” + audit’te önceki/sonraki değer kaydı. Şu an sadece “sonraki” requestData’da; “önceki” snapshot eklenerek audit güçlendirilebilir.

### Catalog ve genişleme

- **Description:** Metadata’da description alanı var; değer şu an null. Key’e göre sabit metin (örn. dictionary) doldurulabilir; UI’da tooltip/label olarak kullanılır.
- **Dinamik catalog:** İleride permission’ları DB’den okumak istersen ayrı bir PermissionDefinition tablosu + catalog endpoint’in oradan beslenmesi gerekir. Mevcut tasarım buna kapalı; ihtiyaç olursa büyük değişiklik.

---

## 4) Karar önerisi

- **Hemen düzeltilmesi gereken:** Login (ve varsa /me) response’taki `permissions` alanının token ile aynı kaynaktan (resolver) üretilmesi. Böylece custom rol kullanan kullanıcıların UI’da da doğru yetkiyle görünmesi sağlanır.
- **Kabul edilebilir risk (izle):** RoleClaims’te çok sayıda permission için round-trip; ilk aşamada kabul edilir, permission sayısı veya güncelleme sıklığı artarsa toplu güncelleme veya ayrı tablo değerlendirilir. Aynı rolü aynı anda iki kişinin düzenlemesi nadir; concurrency için audit’te “önceki/sonraki” snapshot eklenmesi yeterli ilerleme sayılabilir.
- **Sistem/custom ayrımı:** Mevcut isim bazlı, kod sabit listesi şu anki ihtiyaç için uygun. Tenant veya konfig tabanlı “sistem rolü” ihtiyacı çıkarsa o zaman rol meta modeli (ör. IsSystem flag veya ayrı tablo) düşünülür.
- **Genişleme (rename / clone / audit / tenant):**
  - **Rename:** API’de yok; istenirse `PUT /roles/{roleName}` ile `displayName` veya “rename” aksiyonu eklenebilir. Identity’de role name değiştirmek RoleId korunduğu sürece mümkün; mevcut tasarım buna kapalı değil, sadece endpoint yok.
  - **Clone:** Yeni rol oluşturup aynı permission set’i claim olarak kopyalamak; mevcut API (create role + set permissions) ile yapılabilir, tek endpoint “clone” isteğe bağlı kolaylık.
  - **Audit:** ROLE_* aksiyonları var; detay için requestData/description’a eski/yeni permission listesi snapshot’ı eklenebilir.
  - **Tenant:** Mevcut model global rol; tenant bazlı yetki için ya rol adında tenant öneki (TenantA_Cashier) ya da ayrı TenantRolePermission tablosu ve resolver’ın tenant’a göre filtrelemesi gerekir. Bu tasarım tenant’a göre genişlemez; tenant ihtiyacı netleşince ayrı bir tasarım adımı gerekir.

**Özet:** Tasarım sistem/custom ayrımı ve API sınırları açısından tutarlı ve genel olarak doğru. Kritik düzeltme: login/me `permissions` alanını resolver ile beslemek. Diğer noktalar izleme ve ihtiyaç halinde iyileştirme (audit detayı, description, gerekirse performans/concurrency) olarak bırakılabilir.
