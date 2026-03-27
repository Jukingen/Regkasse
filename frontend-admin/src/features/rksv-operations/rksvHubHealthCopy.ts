/**
 * RKSV hub: localized health card copy (mirrors normalizers logic; uses i18n keys under rksvHub.health.*).
 * Türkçe: Sağlık kartı metinleri; normalizer mantığı ile aynı, çeviri anahtarları kullanılır.
 */

import type {
  AdminOperationsSummaryResponse,
  FinanzOnlineMetricsResponse,
  OfflinePayloadHashAnalyzeResult,
} from '@/api/generated/model';
import type { OfflineIntentCoverageSummaryInput } from './normalizers';
import type { OpsHealthLevel } from './types';

export type RksvHubTranslate = (key: string, options?: Record<string, string | number>) => string;

export function getPayloadHealthCopy(
  data: OfflinePayloadHashAnalyzeResult | null | undefined,
  level: OpsHealthLevel,
  t: RksvHubTranslate
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: t('rksvHub.health.payload.summary.unavailable'),
      detailLines: [],
    };
  }
  const lines: string[] = [
    t('rksvHub.health.payload.detail.scanned', { count: data.scanned ?? 0 }),
    t('rksvHub.health.payload.detail.runtimeMismatch', { count: data.runtimeMismatchCount ?? 0 }),
    t('rksvHub.health.payload.detail.conflictGroups', { count: data.conflictGroups?.length ?? 0 }),
    t('rksvHub.health.payload.detail.mismatchRatio', { pct: (data.mismatchRatioPercent ?? 0).toFixed(2) }),
  ];
  if (data.warningMessage) lines.push(t('rksvHub.health.payload.detail.warning', { message: data.warningMessage }));

  let summaryLine = t('rksvHub.health.payload.summary.ok');
  if (level === 'critical') summaryLine = t('rksvHub.health.payload.summary.critical');
  else if (level === 'warning') summaryLine = t('rksvHub.health.payload.summary.warning');
  else if ((data.scanned ?? 0) === 0) summaryLine = t('rksvHub.health.payload.summary.zero');

  return { summaryLine, detailLines: lines };
}

export function getCoverageHealthCopy(
  data: OfflineIntentCoverageSummaryInput | null | undefined,
  level: OpsHealthLevel,
  t: RksvHubTranslate
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: t('rksvHub.health.coverage.summary.unavailable'),
      detailLines: [],
    };
  }
  const lines: string[] = [
    t('rksvHub.health.coverage.detail.total', { count: data.total ?? 0 }),
    t('rksvHub.health.coverage.detail.device', { pct: (data.deviceIdCoveragePercent ?? 0).toFixed(1) }),
    t('rksvHub.health.coverage.detail.sequence', { pct: (data.sequenceCoveragePercent ?? 0).toFixed(1) }),
  ];
  if (data.alertReason) lines.push(t('rksvHub.health.coverage.detail.alertReason', { reason: data.alertReason }));

  const total = data.total ?? 0;
  let summaryLine = t('rksvHub.health.coverage.summary.ok');
  if (level === 'critical') summaryLine = t('rksvHub.health.coverage.summary.critical');
  else if (level === 'warning') summaryLine = t('rksvHub.health.coverage.summary.warning');
  else if (total === 0) summaryLine = t('rksvHub.health.coverage.summary.zero');

  return { summaryLine, detailLines: lines };
}

export function getFinanzOnlineHealthCopy(
  data: FinanzOnlineMetricsResponse | null | undefined,
  level: OpsHealthLevel,
  t: RksvHubTranslate
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: t('rksvHub.health.finanzOnline.summary.unavailable'),
      detailLines: [],
    };
  }
  const lines = [
    t('rksvHub.health.finanzOnline.detail.submitTotal', { count: data.submitTotal ?? 0 }),
    t('rksvHub.health.finanzOnline.detail.failed', { count: data.submitFailedTotal ?? 0 }),
    t('rksvHub.health.finanzOnline.detail.permanent', { count: data.submitFailedPermanent ?? 0 }),
    t('rksvHub.health.finanzOnline.detail.transient', { count: data.submitFailedTransient ?? 0 }),
    t('rksvHub.health.finanzOnline.detail.unknown', { count: data.submitFailedUnknown ?? 0 }),
  ];
  let summaryLine = t('rksvHub.health.finanzOnline.summary.ok');
  if (level === 'critical') summaryLine = t('rksvHub.health.finanzOnline.summary.critical');
  else if (level === 'warning') summaryLine = t('rksvHub.health.finanzOnline.summary.warning');
  return { summaryLine, detailLines: lines };
}

export function getReplayHealthCopy(
  data: AdminOperationsSummaryResponse | null | undefined,
  level: OpsHealthLevel,
  t: RksvHubTranslate
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: t('rksvHub.health.replay.summary.unavailable'),
      detailLines: [],
    };
  }
  const lines = [
    t('rksvHub.health.replay.detail.backlog', {
      backlog: data.replayBacklogCount ?? 0,
      pending: data.replayPendingCount ?? 0,
      failed: data.replayFailedCount ?? 0,
    }),
    t('rksvHub.health.replay.detail.finalFailure', { count: data.replayFinalFailureAuditCount ?? 0 }),
    t('rksvHub.health.replay.detail.synced', { count: data.replaySyncedAuditCount ?? 0 }),
    t('rksvHub.health.replay.detail.incidents', { count: data.incidentCorrelationCount ?? 0 }),
  ];
  let summaryLine = t('rksvHub.health.replay.summary.ok', { hours: data.windowHours ?? 24 });
  if (level === 'critical') summaryLine = t('rksvHub.health.replay.summary.critical');
  else if (level === 'warning') summaryLine = t('rksvHub.health.replay.summary.warning');
  return { summaryLine, detailLines: lines };
}

export function getExportRiskHealthCopy(
  data: AdminOperationsSummaryResponse | null | undefined,
  level: OpsHealthLevel,
  t: RksvHubTranslate
): { summaryLine: string; detailLines: string[] } {
  if (level === 'unavailable' || data == null) {
    return {
      summaryLine: t('rksvHub.health.export.summary.unavailable'),
      detailLines: [],
    };
  }
  const risk = data.exportRisk;
  const lines = [
    t('rksvHub.health.export.detail.total', { count: risk?.totalRiskCount ?? 0 }),
    t('rksvHub.health.export.detail.sequence', {
      dup: risk?.sequenceDuplicateCount ?? 0,
      nonMono: risk?.sequenceNonMonotonicCount ?? 0,
    }),
    t('rksvHub.health.export.detail.orphanRefund', { count: risk?.orphanRefundCount ?? 0 }),
    t('rksvHub.health.export.detail.paymentWithoutInvoice', { count: risk?.paymentWithoutInvoiceCount ?? 0 }),
  ];
  const summaryLine =
    level === 'critical'
      ? t('rksvHub.health.export.summary.critical')
      : t('rksvHub.health.export.summary.ok');
  return { summaryLine, detailLines: lines };
}
