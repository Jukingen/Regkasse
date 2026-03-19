# Device/Sequence Coverage Guard — Actionable Metrics

Coverage metriği artık sadece ölçüm değil; eşik aşımında alert, kasa bazlı risk skoru ve export özeti ile karar üretir.

## 1. Düşük coverage threshold → Alert

- **Konfigürasyon:** `appsettings.json` → `CoverageGuard`
  - `LowCoverageThresholdPercent`: Coverage (DeviceId veya Sequence) bu yüzdenin altına düşerse alert. Varsayılan **80**.
  - `MinSamplesForAlert`: Bu sayıdan az sample varsa alert üretilmez. Varsayılan **10**.
  - `WriteAlertToAuditLog`: `true` ise alert ayrıca audit log’a yazılır (action: `OfflineCoverageLow`).

- **Tetikleyici:** `GET /api/admin/offline-intent-coverage` çağrıldığında (veya export sırasında aynı mantık kullanılır):
  - Toplam sample ≥ MinSamplesForAlert ve (DeviceId coverage % < threshold VEYA Sequence coverage % < threshold) → **LowCoverageAlert = true**
  - Log: `LogWarning` ile oranlar ve threshold yazılır.
  - İsteğe bağlı: Audit’e `OfflineCoverageLow` event’i eklenir.

- **Response alanları:** `LowCoverageAlert`, `AlertReason`, `DeviceIdCoveragePercent`, `SequenceCoveragePercent`.

## 2. Kasa bazlı risk skoru

- **Risk skoru:** `DeviceIdMissingRate + SequenceMissingRate` (0..2). Yüksek = daha riskli.
- **GET /api/admin/offline-intent-coverage** cevabında `ByRegister` içinde her kasa için:
  - `DeviceIdMissingRate`, `SequenceMissingRate`, `RiskScore`.

## 3. Top riskli N kasa endpoint’i

- **GET** `/api/admin/offline-intent-coverage/top-risk`
  - **Query:** `fromUtc`, `toUtc` (varsayılan son 24 saat), `limit` (varsayılan 10, max 100).
  - **Cevap:** `OfflineIntentCoverageTopRiskResponse` → `Registers`: Risk skoruna göre azalan sırada (en riskli önce), her kayıtta `CashRegisterId`, `Total`, `WithDeviceId`, `WithSequence`, `DeviceIdMissingRate`, `SequenceMissingRate`, `RiskScore`.

## 4. Export’e summary ekleme

- **Fiscal export** (`Integrity`):
  - `DeviceIdCoveragePercent`, `SequenceCoveragePercent`: Dönem içi coverage yüzdeleri (yoksa null).
  - `LowCoverageAlert`: Bu kasa için coverage threshold altındaysa true.
  - `IntegrityDiagnosticNotes`: Low coverage uyarısı ve threshold bilgisi eklenir.

Aynı threshold/min sample kuralları export için de kullanılır (`CoverageGuard` options).

## Konfigürasyon özeti

```json
"CoverageGuard": {
  "LowCoverageThresholdPercent": 80,
  "MinSamplesForAlert": 10,
  "WriteAlertToAuditLog": true
}
```

## Amaç

Sadece ölçmek değil, karar üretmek: dashboard’da veya export’ta düşük coverage görüldüğünde alert ve “en riskli kasalar” listesi ile aksiyon alınabilir (mobil güncelleme, inceleme, rollout önceliği).
