# Offline replay — structural fallback sadeleştirmesi

**Kaldırma planı:** Structural fallback’i tamamen kaldırmak için ön koşullar, metrik, feature-flag rollout ve PR adımları → **`STRUCTURAL_FALLBACK_REMOVAL_PLAN.md`**.

**Fallback kullanım metriği:** Prometheus `structural_fallback_resolved_total`, `structural_fallback_ambiguous_total`; log’da *"Offline resolved by structural fallback"* araması alternatiftir.

---

## 1. Structural fallback akışı (sadeleştirme öncesi)

**Ne zaman çalışıyordu:** Replay sırasında sırayla:

1. **Requested Id** ile satır bulunur → bulunursa kullanılır.
2. **(CashRegisterId, PayloadHash)** ile bulunur → bulunursa dedup audit, kullanılır.
3. **Runtime recomputed hash:** Aynı register’da son 2000 satırda `PayloadJson` üzerinden runtime canonical hash hesaplanır; gelen `payloadHash` ile eşleşen ilk satır alınır (legacy: DB’de `payload_hash` bazen eski backfill ile farklıydı).
4. **Structural fallback:** Hâlâ bulunamadıysa son **150** satırda `JsonNode.DeepEquals(stored.PayloadJson, normalizedPayloadJson)` ile **ilk eşleşen** satır alınırdı.
5. Hiçbiri yoksa **yeni** `OfflineTransaction` satırı oluşturulur.

**Koşul:** Adım 4 yalnızca 2 ve 3’ten sonuç alınamadığında devreye giriyordu. Yani doğrudan hash veya recomputed hash ile eşleşme yoksa, “son çare” olarak structural eşleşme kullanılıyordu.

---

## 2. Risk değerlendirmesi

- **Yanlış eşleşme:** Aynı pencerede birden fazla satır aynı payload’a structural olarak eşleşebilir (ör. aynı içerikle iki farklı intent). `FirstOrDefault` ile **en yeni** (CreatedAt desc) alınıyordu; farklı iki intent aynı JSON’a sahipse yanlış satıra bağlanma riski vardı.
- **Debugging:** Structural ile çözüldüğü audit/log’da açık değildi; sadece PAYLOAD_HASH_DEDUPLICATED yazılıyordu.
- **Karmaşıklık:** 150 satır tam yüklenip bellekte DeepEquals ile taranıyordu; pencerenin büyüklüğü ve “ilk eşleşen” tekliği garanti etmiyordu.

---

## 3. Sadeleştirme sonrası akış

- **1–3:** Aynı (requested Id → hash dedup → runtime recomputed hash).
- **4. Structural fallback (daraltıldı ve guard’lı):**
  - **Kill-switch:** `OfflineReplay:AllowStructuralFallback` (config) **false** ise adım 4 **hiç çalışmaz**; legacy repair sonrası kapatılabilir.
  - **Pencere:** Sabit 150 yerine **config’den** `StructuralPayloadFallbackLimit` (varsayılan **50**); üst sınır 500.
  - **Deterministik guard:** Eşleşen satır sayısı **tam 1** değilse (0 veya 2+) structural ile **hiç resolve edilmez**; yeni satır oluşturulur. Böylece “tek bir doğru eşleşme” garantilenir.
  - **Log:** Structural ile resolve edildiğinde `Information`: *"Offline resolved by structural fallback: CashRegisterId=..., OfflineId=... (hash path did not match)."* Ambiguous durumda `Debug`: *"Offline structural fallback: ambiguous match for CashRegisterId=..., N rows match; skipping."*

---

## 4. Kaldırılan / azaltılan karmaşıklık

| Önceki | Sonraki |
|--------|--------|
| Sabit 150 satır, ilk eşleşen alınıyor | Config’den limit (varsayılan 50, max 500), **yalnızca tek eşleşme** alınıyor |
| Structural her zaman açık | `AllowStructuralFallback` ile kapatılabilir (legacy sonrası kill-switch) |
| Structural kullanımı log’da görünmüyordu | Information/Debug log ile izlenebilir |
| Çoklu eşleşmede “en yeni” seçimi (risk) | Çoklu eşleşmede resolve yapılmıyor; yeni satır açılır |

---

## 5. Kalan riskler

- **Legacy veri:** Eski `payload_hash` backfill’i tam bitmeden `AllowStructuralFallback = false` yapılırsa, hash ve recompute ile eşleşmeyen ama structural olarak aynı olan satırlar “bulunamadı” sayılıp **yeni satır** oluşturulur (duplicate intent riski). Bu yüzden kill-switch yalnızca legacy repair tamamlandıktan sonra açılmalı.
- **Correctness:** Tek structural eşleşmede davranış önceki “ilk eşleşen” ile aynı; çoklu eşleşmede artık tahmin yok, yeni satır açılıyor. Doğruluk bozulmaz; sadece belirsiz durumda daha güvenli taraf seçilir.

---

## 6. Güvenli rollout stratejisi

1. **Varsayılan:** `AllowStructuralFallback = true`, `StructuralPayloadFallbackLimit = 50`. Mevcut davranış korunur, pencere küçülür, ambiguous guard devrede.
2. **İzleme:** Log’da *"Offline resolved by structural fallback"* aramak; ne sıklıkta kullanıldığını görmek.
3. **Legacy repair:** Tüm ilgili satırlarda `payload_hash` runtime canonical ile hizalandıktan sonra (örn. maintenance job veya backfill script), structural’a ihtiyaç kalmayabilir.
4. **Kill-switch:** Artık structural gerekmediği doğrulandıktan sonra `appsettings.json` veya ortam değişkeni ile `OfflineReplay:AllowStructuralFallback = false` yapın. Böylece adım 4 tamamen devre dışı kalır.

---

## 7. Değişen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Options/OfflineReplayOptions.cs` (namespace `KasseAPI_Final.Configuration`) | Yeni: `AllowStructuralFallback`, `StructuralPayloadFallbackLimit`. |
| `backend/Services/OfflineTransactionService.cs` | `IOptions<OfflineReplayOptions>`, structural yalnızca flag açıksa, limit config’den, **tek eşleşme** guard’ı, structural resolve/ambiguous log. |
| `backend/Program.cs` | `Configure<OfflineReplayOptions>(GetSection("OfflineReplay"))`. |
| `docs/release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md` | Bu rapor. |

Testler: Mevcut offline replay testleri (LegacyWrongPayloadHash dahil) step 3 (recomputed hash) ile çalışıyor; structural’a bağımlı test yok. Constructor’a `IOptions` opsiyonel eklendiği için tüm mevcut testler değişmeden geçer.
