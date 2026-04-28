# Structural fallback — kaldırma analizi ve plan

> **Status:** Verification required before implementation. Treat as release/plan document, not current runtime behavior unless validated.

**Amaç:** Karmaşıklığı azaltmak; structural fallback tamamen kaldırılabilir mi analiz ve kaldırma ön koşulları, feature-flag rollout ve PR planı.

---

## 1. Mevcut durum özeti

- **Structural fallback:** Replay sırasında hash yolu (direct + recomputed) ile offline satır bulunamazsa, son N satırda (varsayılan 50, max 500) **structural JSON eşleşmesi** ile tek bir satır bulunursa o kullanılır; 0 veya 2+ eşleşmede resolve yapılmaz.
- **Konum:** `OfflineTransactionService.TryResolveOfflineByStructuralPayloadAsync`, yalnızca `OfflineReplay:AllowStructuralFallback = true` iken çağrılıyor.
- **Kill-switch:** `AllowStructuralFallback = false` ile adım 4 tamamen devre dışı; kod yolu hâlâ mevcut, sadece giriş yapılmıyor.

**Kaldırma sorusu:** Bu kod yolunu (metot + çağrı + config alanları) tamamen silmek mümkün mü? **Evet**, ön koşullar sağlandıktan sonra tamamen kaldırılabilir.

---

## 2. Fallback usage metriği

### 2.1 Counter (uygulandı)

- **Prometheus:**
  - `structural_fallback_resolved_total`: Hash yolu eşleşmeyen, structural ile **tek eşleşme** bulunup resolve edilen replay sayısı.
  - `structural_fallback_ambiguous_total`: Structural fallback’te **birden fazla** eşleşme bulunup resolve yapılmayan (skip) sayısı.
- **Kullanım:** Grafana’da `rate(structural_fallback_resolved_total[1h])` veya `increase(structural_fallback_resolved_total[7d])` ile kullanım izlenir. Uzun süre 0 ise fallback fiilen kullanılmıyordur.
- **Kod:** `ICoreMetrics.RecordStructuralFallbackResolved` / `RecordStructuralFallbackAmbiguous`, `OfflineTransactionService.TryResolveOfflineByStructuralPayloadAsync` içinde çağrılıyor.

### 2.2 Log tarama (alternatif)

- **Aranacak metin:** `"Offline resolved by structural fallback"` (Information).
- **Ambiguous:** `"Offline structural fallback: ambiguous match"` (Debug).
- **Araç:** Mevcut log toplama (ör. Loki, ELK) ile bu mesajlara göre arama; counter yokken veya geçmiş dönem için kullanılabilir.

**Öneri:** Counter ile metrik toplanıyor; kaldırma öncesi en az 1–2 hafta (tercihen 2–4 hafta) `structural_fallback_resolved_total` artışının 0’a yakın olduğu doğrulanmalı.

---

## 3. Kaldırma ön koşulları

### 3.1 Mismatch oranı düşük olmalı

- **Anlam:** `payload_hash` ile runtime canonical hash uyumsuz satır oranı düşük olmalı; böylece replay çoğunlukla hash yolu (direct veya recomputed) ile çözülecek, structural’a ihtiyaç kalmaz.
- **Ölçüm:**
  - **API:** `POST /api/admin/offline-payload-hash/analyze` (sample size ile) → `MismatchRatioPercent`, `LegacyDataQualityRiskHigh`.
  - **Export / risk:** `GET /api/admin/offline-payload-hash/risk` → mismatch oranı ve risk bayrağı.
  - **Guard:** `PayloadHashGuard:MismatchWarningThresholdPercent` (varsayılan 10); bu eşiğin altında olmak “düşük” için makul hedef.
- **Koşul (önerilen):**
  1. `MismatchRatioPercent` hedef: **&lt; %5** (tercihen %1’e yakın veya 0).
  2. Gerekirse **repair:** `POST /api/admin/offline-payload-hash/repair` (dry-run sonrası gerçek) ile uyumsuz satırlar hizalandıktan sonra tekrar analyze.
  3. Lazy repair (replay sırasında hizalama) zaten var; repair sonrası yeni replay’ların çoğu hash ile çözülecektir.

### 3.2 Fallback kullanımı ihmal edilebilir

- **Metrik:** `structural_fallback_resolved_total` artışı (ör. son 2–4 hafta) **0’a yakın** veya toplam replay’a göre çok düşük (ör. &lt; %0,1).
- **Log:** Production’da “Offline resolved by structural fallback” çok nadir veya hiç görülmemeli.

Bu iki koşul sağlandığında structural fallback’i kapatmak ve sonra kaldırmak güvenlidir.

---

## 4. Feature flag rollout planı

Mevcut flag: **`OfflineReplay:AllowStructuralFallback`** (zaten var).

| Aşama | Ne yapılır | Doğrulama |
|-------|------------|-----------|
| **0. Metrik** | Counter’lar açık (`structural_fallback_resolved_total`, `structural_fallback_ambiguous_total`). | Grafana’da metrik görünür, bir süre veri toplanır. |
| **1. Mismatch düşürme** | Analyze → repair (gerekirse) → tekrar analyze. MismatchRatioPercent &lt; %5 (tercihen ~0). | Risk endpoint ve analyze response. |
| **2. İzleme** | En az 2–4 hafta production’da replay + fallback metrikleri izlenir. | `structural_fallback_resolved_total` artışı ~0. |
| **3. Flag kapatma** | Canlıda `AllowStructuralFallback: false` (önce tek kasa/ortam, sonra tümü). | Replay başarı oranı ve hata logları değişmemeli; fallback metrikleri artmaz. |
| **4. Sabit kapalı** | Tüm ortamlarda flag false; bir release boyunca sorun yoksa kaldırma PR’ına geçilir. | Incident yok, replay davranışı aynı. |
| **5. Kod kaldırma** | Aşağıdaki “Fallback removal PR” uygulanır. | Testler yeşil, regression yok. |

**Geri alma:** Flag tekrar `true` yapılarak structural fallback yeniden açılabilir; kod kaldırıldıktan sonra geri almak için yeniden kod gerekir (bu yüzden 4. aşama önemli).

---

## 5. Fallback removal PR (kod kaldırma)

### 5.1 Kaldırılacaklar

| Yer | Değişiklik |
|-----|------------|
| `OfflineTransactionService.cs` | `TryResolveOfflineByStructuralPayloadAsync` metodu **tamamen silinir**. |
| `OfflineTransactionService.cs` | Hash/recomputed sonrası “4) structural fallback” bloğu: `if (offline == null && _replayOptions.AllowStructuralFallback)` ve içindeki `TryResolveOfflineByStructuralPayloadAsync` çağrısı + dedup audit bloğu **kaldırılır**. |
| `OfflineReplayOptions.cs` | `AllowStructuralFallback` ve `StructuralPayloadFallbackLimit` property’leri **kaldırılır**. |
| `appsettings.json` | `OfflineReplay` altında bu iki ayar varsa **kaldırılır** (diğer OfflineReplay ayarları kalır). |

### 5.2 Metrikler (opsiyonel)

- **Seçenek A:** Structural fallback tamamen gittiği için `RecordStructuralFallbackResolved` / `RecordStructuralFallbackAmbiguous` çağrıları da kalkar; **counter’ları Prometheus’tan kaldırmak** isteğe bağlı (eski veri kalır, yeni artış olmaz).
- **Seçenek B:** Counter’ları ve interface metodlarını da **silerek** tam sadeleştirme yapılabilir.

PR açıklamasında: “Structural fallback removal; preconditions (low mismatch rate, zero fallback usage) verified; AllowStructuralFallback no longer used.”

### 5.3 Doküman güncellemeleri

- `OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md`: “Structural fallback kaldırıldı” notu; artık sadece tarihsel referans.
- `LEGACY_PAYLOAD_HASH_MISMATCH.md`: “Disabling structural fallback” yerine “Structural fallback has been removed”.
- `TECH_REVIEW_BACKLOG.md`: P2.1 satırı “Removed” veya “Done (removed)” olarak güncellenir.

### 5.4 Testler

- Mevcut replay testleri hash/recomputed path’e dayanıyor; structural’a özel test yok. **Regression:** Tüm offline replay testleri çalıştırılır; başarılı olmalı.
- İsteğe bağlı: `AllowStructuralFallback = false` ile replay’ın “create new row” veya “recomputed match” senaryolarında aynı sonucu verdiği bir entegrasyon testi eklenebilir (zaten recompute path testleri var).

---

## 6. Özet

| Soru | Cevap |
|------|--------|
| Structural fallback tamamen kaldırılabilir mi? | **Evet**, mismatch oranı düşük ve fallback kullanımı ihmal edilebilir olduktan sonra. |
| Fallback usage metriği | **Counter:** `structural_fallback_resolved_total`, `structural_fallback_ambiguous_total` (Prometheus). **Alternatif:** Log’da “Offline resolved by structural fallback” taraması. |
| Kaldırma ön koşulları | Mismatch oranı düşük (analyze/repair, hedef &lt; %5); fallback metrik artışı ~0 (2–4 hafta). |
| Feature flag rollout | Mevcut `AllowStructuralFallback`; önce metrik + mismatch düşürme → izleme → flag false → sabit kalma → removal PR. |
| Fallback removal PR | `TryResolveOfflineByStructuralPayloadAsync` ve çağrısı silinir; options’tan iki property kaldırılır; config ve docs güncellenir. |

Bu plan ile karmaşıklık azaltılır ve tek çözüm yolu (hash + recompute) kalır.
