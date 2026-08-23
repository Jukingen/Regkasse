# Device/sequence coverage guard — actionable metrics

Coverage is no longer measurement-only. When a threshold is crossed, the system produces an alert, a per-register risk score, and an export summary so operators can act.

## 1. Low-coverage threshold → alert

- **Configuration:** `appsettings.json` → `CoverageGuard`
  - `LowCoverageThresholdPercent`: Alert when DeviceId or Sequence coverage falls below this percent. Default **80**.
  - `MinSamplesForAlert`: Do not alert below this sample count. Default **10**.
  - `WriteAlertToAuditLog`: When `true`, also write the alert to the audit log (action: `OfflineCoverageLow`).

- **Trigger:** On `GET /api/admin/offline-intent-coverage` (export uses the same logic):
  - Total samples ≥ MinSamplesForAlert **and** (DeviceId coverage % < threshold **or** Sequence coverage % < threshold) → **LowCoverageAlert = true**
  - Log: `LogWarning` with ratios and threshold.
  - Optional: add an `OfflineCoverageLow` audit event.

- **Response fields:** `LowCoverageAlert`, `AlertReason`, `DeviceIdCoveragePercent`, `SequenceCoveragePercent`.

## 2. Per-register risk score

- **Risk score:** `DeviceIdMissingRate + SequenceMissingRate` (0..2). Higher = riskier.
- On `GET /api/admin/offline-intent-coverage`, each register in `ByRegister` includes:
  - `DeviceIdMissingRate`, `SequenceMissingRate`, `RiskScore`.

## 3. Top-N riskiest registers endpoint

- **GET** `/api/admin/offline-intent-coverage/top-risk`
  - **Query:** `fromUtc`, `toUtc` (default last 24 hours), `limit` (default 10, max 100).
  - **Response:** `OfflineIntentCoverageTopRiskResponse` → `Registers`: descending risk score (riskiest first), each with `CashRegisterId`, `Total`, `WithDeviceId`, `WithSequence`, `DeviceIdMissingRate`, `SequenceMissingRate`, `RiskScore`.

## 4. Summary on export

- **Fiscal export** (`Integrity`):
  - `DeviceIdCoveragePercent`, `SequenceCoveragePercent`: coverage percents for the period (null if none).
  - `LowCoverageAlert`: true when this register is below the coverage threshold.
  - `IntegrityDiagnosticNotes`: includes the low-coverage warning and threshold.

Export uses the same threshold / min-sample rules (`CoverageGuard` options).

## Configuration summary

```json
"CoverageGuard": {
  "LowCoverageThresholdPercent": 80,
  "MinSamplesForAlert": 10,
  "WriteAlertToAuditLog": true
}
```

## Purpose

Measure **and** decide: when dashboard or export shows low coverage, alert plus a “riskiest cash registers” list so operators can act (mobile update, investigation, rollout priority).
