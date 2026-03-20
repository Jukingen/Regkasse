/**
 * Pure mappers from backend DTOs to dashboard health levels (RKSV operations landing).
 */

import type {
  AdminOperationsSummaryResponse,
  OfflinePayloadHashAnalyzeResult,
  FinanzOnlineMetricsResponse,
} from '@/api/generated/model';
import type { OpsHealthLevel } from './types';

/** Coverage summary shape from GET /api/admin/offline-intent-coverage */
export interface OfflineIntentCoverageSummaryInput {
  lowCoverageAlert?: boolean;
  alertReason?: string | null;
  deviceIdCoveragePercent?: number;
  sequenceCoveragePercent?: number;
  total?: number;
}

export function mapPayloadHashAnalyzeToHealth(data: OfflinePayloadHashAnalyzeResult | null | undefined): OpsHealthLevel {
  if (data == null) return 'unavailable';
  const conflicts = data.conflictGroups?.length ?? 0;
  const runtimeMismatch = data.runtimeMismatchCount ?? 0;
  if (data.legacyDataQualityRiskHigh || conflicts > 0 || runtimeMismatch > 0) return 'critical';
  const ratio = data.mismatchRatioPercent ?? 0;
  const nullHashes = data.nullOrEmptyPayloadHash ?? 0;
  const repairable = data.repairableNoConflictCount ?? 0;
  const skippedConflict = data.skippedWouldConflictCount ?? 0;
  if (ratio > 0 || nullHashes > 0 || repairable > 0 || skippedConflict > 0) return 'warning';
  return 'healthy';
}

export function buildPayloadHashCardCopy(
  data: OfflinePayloadHashAnalyzeResult | null | undefined,
  level: OpsHealthLevel
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine:
        'API-Fehler oder keine Antwort — kein Signal (nicht gleichbedeutend mit „keine Probleme“).',
      detailLines: [],
    };
  }
  const lines: string[] = [
    `Gescannt: ${data.scanned}`,
    `Mismatch (Runtime): ${data.runtimeMismatchCount ?? 0}`,
    `Konfliktgruppen: ${data.conflictGroups?.length ?? 0}`,
    `Mismatch-Quote: ${(data.mismatchRatioPercent ?? 0).toFixed(2)}%`,
  ];
  if (data.warningMessage) lines.push(data.warningMessage);
  let summaryLine =
    'Keine Auffälligkeiten in dieser Analyse — nur Stichprobe (max. Zeilen), kein vollständiger Datenbestand.';
  if (level === 'critical') {
    summaryLine = 'Kritische Payload-Hash-Risiken in der Stichprobe — triagieren.';
  } else if (level === 'warning') {
    summaryLine = 'Hinweise in der Stichprobe — Detailseite und ggf. größeren Umfang prüfen.';
  } else if ((data.scanned ?? 0) === 0) {
    summaryLine =
      '0 Zeilen in der Stichprobe — kein Bewertungssignal (nicht „alles in Ordnung“).';
  }
  return { summaryLine, detailLines: lines };
}

export function mapCoverageSummaryToHealth(data: OfflineIntentCoverageSummaryInput | null | undefined): OpsHealthLevel {
  if (data == null) return 'unavailable';
  if (data.lowCoverageAlert) return 'critical';
  const d = data.deviceIdCoveragePercent ?? 100;
  const s = data.sequenceCoveragePercent ?? 100;
  if (d < 95 || s < 95) return 'warning';
  return 'healthy';
}

export function buildCoverageCardCopy(
  data: OfflineIntentCoverageSummaryInput | null | undefined,
  level: OpsHealthLevel
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine:
        'Coverage-API fehlgeschlagen — kein Signal (nicht gleichbedeutend mit „keine Probleme“).',
      detailLines: [],
    };
  }
  const lines: string[] = [
    `Samples (Zeitraum): ${data.total ?? 0}`,
    `DeviceId: ${(data.deviceIdCoveragePercent ?? 0).toFixed(1)}%`,
    `Sequenz: ${(data.sequenceCoveragePercent ?? 0).toFixed(1)}%`,
  ];
  if (data.alertReason) lines.push(`Alert: ${data.alertReason}`);
  const total = data.total ?? 0;
  let summaryLine =
    'Coverage laut API im grünen Bereich (letzte 24h UTC, siehe Detailseite für Filter).';
  if (level === 'critical') summaryLine = 'Coverage-Alert laut API — Details prüfen.';
  else if (level === 'warning') summaryLine = 'Coverage unter Schwellen — Details prüfen.';
  else if (total === 0) {
    summaryLine = '0 Samples im Zeitraum — kein Coverage-Signal (nicht „alles in Ordnung“).';
  }
  return { summaryLine, detailLines: lines };
}

export function mapFinanzOnlineMetricsToHealth(data: FinanzOnlineMetricsResponse | null | undefined): OpsHealthLevel {
  if (data == null) return 'unavailable';
  const permanent = data.submitFailedPermanent ?? 0;
  if (permanent > 0) return 'critical';
  const failed = data.submitFailedTotal ?? 0;
  if (failed > 0) return 'warning';
  return 'healthy';
}

export function buildFinanzOnlineCardCopy(
  data: FinanzOnlineMetricsResponse | null | undefined,
  level: OpsHealthLevel
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine:
        'Metriken-API fehlgeschlagen — kein Signal (nicht gleichbedeutend mit „keine Probleme“).',
      detailLines: [],
    };
  }
  const lines = [
    `Submits gesamt: ${data.submitTotal}`,
    `Fehlgeschlagen: ${data.submitFailedTotal}`,
    `Permanent: ${data.submitFailedPermanent}`,
    `Transient: ${data.submitFailedTransient}`,
    `Unbekannt: ${data.submitFailedUnknown}`,
  ];
  let summaryLine = 'Laut Metriken-API: keine fehlgeschlagenen Submits (Zähler).';
  if (level === 'critical') summaryLine = 'Permanente FO-Submit-Fehler laut Metriken — Abgleich prüfen.';
  else if (level === 'warning') summaryLine = 'Fehlgeschlagene Submits laut Metriken — Abgleich prüfen.';
  return { summaryLine, detailLines: lines };
}

export function mapReplaySummaryToHealth(data: AdminOperationsSummaryResponse | null | undefined): OpsHealthLevel {
  if (data == null) return 'unavailable';
  const backlog = data.replayBacklogCount ?? 0;
  const finalFailures = data.replayFinalFailureAuditCount ?? 0;
  if (finalFailures > 0 || backlog > 50) return 'critical';
  if (backlog > 0 || (data.replayFailedCount ?? 0) > 0) return 'warning';
  return 'healthy';
}

export function buildReplaySummaryCardCopy(
  data: AdminOperationsSummaryResponse | null | undefined,
  level: OpsHealthLevel
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: 'Replay-Übersicht nicht verfügbar — keine belastbare Aussage.',
      detailLines: [],
    };
  }
  const lines = [
    `Backlog: ${data.replayBacklogCount ?? 0} (Pending ${data.replayPendingCount ?? 0}, Failed ${data.replayFailedCount ?? 0})`,
    `Final-Failure-Audit (Fenster): ${data.replayFinalFailureAuditCount ?? 0}`,
    `OFFLINE_SYNCED (Fenster): ${data.replaySyncedAuditCount ?? 0}`,
  ];
  let summaryLine = `Replay unauffällig im ${data.windowHours ?? 24}h-Fenster.`;
  if (level === 'critical') summaryLine = 'Replay-Risiko erhöht (Backlog oder Final-Failure-Audit).';
  else if (level === 'warning') summaryLine = 'Replay benötigt Aufmerksamkeit (offene/fehlgeschlagene Elemente).';
  return { summaryLine, detailLines: lines };
}

export function mapExportRiskToHealth(data: AdminOperationsSummaryResponse | null | undefined): OpsHealthLevel {
  if (data == null) return 'unavailable';
  const total = data.exportRisk?.totalRiskCount ?? 0;
  if (total > 0) return 'critical';
  return 'healthy';
}

export function buildExportRiskCardCopy(
  data: AdminOperationsSummaryResponse | null | undefined,
  level: OpsHealthLevel
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: 'Export-/Integritätsübersicht nicht verfügbar — keine belastbare Aussage.',
      detailLines: [],
    };
  }
  const risk = data.exportRisk;
  const lines = [
    `Risiken gesamt: ${risk?.totalRiskCount ?? 0}`,
    `Seq duplicate/non-monotonic: ${risk?.sequenceDuplicateCount ?? 0}/${risk?.sequenceNonMonotonicCount ?? 0}`,
    `Orphan refunds: ${risk?.orphanRefundCount ?? 0}`,
    `Payments ohne Invoice: ${risk?.paymentWithoutInvoiceCount ?? 0}`,
  ];
  const summaryLine =
    level === 'critical'
      ? 'Export-/Integritätsrisiken vorhanden — Integritätsseite prüfen.'
      : 'Keine Export-/Integritätsrisiken im Summary-Check.';
  return { summaryLine, detailLines: lines };
}

export function healthTagColor(level: OpsHealthLevel): string {
  switch (level) {
    case 'healthy':
      return 'success';
    case 'warning':
      return 'warning';
    case 'critical':
      return 'error';
    default:
      return 'default';
  }
}

export function healthLabelDe(level: OpsHealthLevel): string {
  switch (level) {
    case 'healthy':
      return 'OK';
    case 'warning':
      return 'Hinweis';
    case 'critical':
      return 'Kritisch';
    default:
      return 'Nicht verfügbar';
  }
}
