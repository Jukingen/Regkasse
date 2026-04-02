/**
 * Restore drill hata kodları: Fake/stub kaynaklı pg_restore --list başarısızlığını operatör dilinde açıklar.
 * Backend `DetailsJson` içinde `pgRestoreListFailureContext` ile işaretler (API stderr ham kalır).
 */

import type { RestoreVerificationRunResponseDto } from '@/api/generated/model';

export const PG_RESTORE_LIST_FAILED = 'PG_RESTORE_LIST_FAILED';

/** Backend RestoreVerificationOrchestratorHostedService ile uyumlu sabit. */
export const FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT = 'fake_adapter_stub_not_pg_restore_format';

/**
 * `PG_RESTORE_LIST_FAILED` için UI yorumu — Fake stub beklenen başarısızlık mı, gerçek pg_dump hattında risk mi.
 * Kanıt: `detailsJson` içi reason, adaptör sezgisi, `pgRestoreListExitCode`, `failureDetail` (PgRestoreListInspector stderr).
 */
export type PgRestoreListFailureKind =
  | 'fake_stub_expected'
  | 'real_pg_restore_unavailable'
  | 'real_dump_file_missing'
  | 'real_format_or_corrupt'
  | 'real_unknown';

export type PgRestoreListFailureInterpretation = {
  kind: PgRestoreListFailureKind;
};

export type PgRestoreListRunFields = Pick<
  RestoreVerificationRunResponseDto,
  'failureCode' | 'failureDetail' | 'detailsJson' | 'pgRestoreListExitCode' | 'dumpInspectionPassed'
>;

export type PgRestoreListFailureContext = {
  reason?: string;
  sourceAdapterKind?: string;
  sourceBackupRunId?: string;
};

export function parsePgRestoreListFailureContext(
  detailsJson: string | null | undefined,
): PgRestoreListFailureContext | null {
  if (!detailsJson?.trim()) return null;
  try {
    const o = JSON.parse(detailsJson) as Record<string, unknown>;
    const raw = o.pgRestoreListFailureContext;
    if (!raw || typeof raw !== 'object') return null;
    const ctx = raw as Record<string, unknown>;
    return {
      reason: typeof ctx.reason === 'string' ? ctx.reason : undefined,
      sourceAdapterKind: typeof ctx.sourceAdapterKind === 'string' ? ctx.sourceAdapterKind : undefined,
      sourceBackupRunId: typeof ctx.sourceBackupRunId === 'string' ? ctx.sourceBackupRunId : undefined,
    };
  } catch {
    return null;
  }
}

/**
 * Fake/stub mantıksal “dump” dosyası PostgreSQL özel biçimi değil; pg_restore --list beklenen şekilde düşer.
 * Önce API’deki bağlam (DetailsJson), yoksa yapılandırma + Fake adaptör sezgisel sinyali.
 */
export function shouldShowFakeStubPgRestoreListExplainer(params: {
  failureCode: string | null | undefined;
  detailsJson: string | null | undefined;
  /** Yapılandırma/son çalıştırma Fake veya simüle mi — DetailsJson yoksa yedek sinyal. */
  isSimulatedPipelineHeuristic: boolean;
}): boolean {
  if (params.failureCode !== PG_RESTORE_LIST_FAILED) return false;
  const ctx = parsePgRestoreListFailureContext(params.detailsJson);
  if (ctx?.reason === FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT) return true;
  return params.isSimulatedPipelineHeuristic;
}

export function interpretPgRestoreListFailure(params: {
  run: PgRestoreListRunFields;
  isSimulatedPipelineHeuristic: boolean;
}): PgRestoreListFailureInterpretation | null {
  if (params.run.failureCode !== PG_RESTORE_LIST_FAILED) return null;

  const fake = shouldShowFakeStubPgRestoreListExplainer({
    failureCode: params.run.failureCode,
    detailsJson: params.run.detailsJson,
    isSimulatedPipelineHeuristic: params.isSimulatedPipelineHeuristic,
  });
  if (fake) return { kind: 'fake_stub_expected' };

  const detail = (params.run.failureDetail ?? '').trim();
  const exit = params.run.pgRestoreListExitCode;

  if (exit === -1) {
    if (/failed to start pg_restore/i.test(detail)) return { kind: 'real_pg_restore_unavailable' };
    if (/dump file missing/i.test(detail)) return { kind: 'real_dump_file_missing' };
    return { kind: 'real_unknown' };
  }

  if (exit != null && exit !== 0) {
    return { kind: 'real_format_or_corrupt' };
  }

  return { kind: 'real_unknown' };
}

/** Üst banner + Alerts satırı için aynı anlatı anahtarı (Fake hariç Alerts’te tekrar). */
export function pgRestoreListFailureKindToBannerMessageKey(kind: PgRestoreListFailureKind): {
  tier: 'info' | 'warn' | 'critical';
  key: string;
} {
  switch (kind) {
    case 'fake_stub_expected':
      return { tier: 'info', key: 'backupDr.banner.restoreDrillStubListFailedExpected' };
    case 'real_pg_restore_unavailable':
      return { tier: 'warn', key: 'backupDr.banner.restoreDrillRealListFailedPgRestoreUnavailable' };
    case 'real_dump_file_missing':
      return { tier: 'warn', key: 'backupDr.banner.restoreDrillRealListFailedDumpMissing' };
    case 'real_format_or_corrupt':
      return { tier: 'critical', key: 'backupDr.banner.restoreDrillRealListFailedBadArchive' };
    case 'real_unknown':
      return { tier: 'critical', key: 'backupDr.banner.restoreDrillRealListFailedUnknown' };
  }
}

export function pgRestoreListFailureKindToStatusLabelKey(kind: PgRestoreListFailureKind): string {
  const m: Record<PgRestoreListFailureKind, string> = {
    fake_stub_expected: 'backupDr.restoreStatus.drillStubExpected',
    real_pg_restore_unavailable: 'backupDr.restoreStatus.drillListFailedPgRestoreUnavailable',
    real_dump_file_missing: 'backupDr.restoreStatus.drillListFailedDumpMissing',
    real_format_or_corrupt: 'backupDr.restoreStatus.drillListFailedBadArchive',
    real_unknown: 'backupDr.restoreStatus.drillListFailedUnknown',
  };
  return m[kind];
}

export function pgRestoreListFailureKindToTagColor(kind: PgRestoreListFailureKind): string {
  switch (kind) {
    case 'fake_stub_expected':
      return 'blue';
    case 'real_pg_restore_unavailable':
    case 'real_dump_file_missing':
      return 'orange';
    case 'real_format_or_corrupt':
    case 'real_unknown':
      return 'red';
  }
}

type RestoreVerificationAlertTone = 'info' | 'warning' | 'error';

export function pgRestoreListFailureKindToCardAlertKeys(kind: PgRestoreListFailureKind): {
  tone: RestoreVerificationAlertTone;
  titleKey: string;
  bodyKey: string;
} {
  switch (kind) {
    case 'fake_stub_expected':
      return {
        tone: 'info',
        titleKey: 'backupDr.restoreVerification.fakePipeline.drillOutcomeTitle',
        bodyKey: 'backupDr.restoreVerification.fakePipeline.pgRestoreListExplainer',
      };
    case 'real_pg_restore_unavailable':
      return {
        tone: 'warning',
        titleKey: 'backupDr.restoreVerification.realPipeline.listFailedPgRestoreUnavailableTitle',
        bodyKey: 'backupDr.restoreVerification.realPipeline.listFailedPgRestoreUnavailableBody',
      };
    case 'real_dump_file_missing':
      return {
        tone: 'warning',
        titleKey: 'backupDr.restoreVerification.realPipeline.listFailedDumpMissingTitle',
        bodyKey: 'backupDr.restoreVerification.realPipeline.listFailedDumpMissingBody',
      };
    case 'real_format_or_corrupt':
      return {
        tone: 'error',
        titleKey: 'backupDr.restoreVerification.realPipeline.listFailedFormatRejectedTitle',
        bodyKey: 'backupDr.restoreVerification.realPipeline.listFailedFormatRejectedBody',
      };
    case 'real_unknown':
      return {
        tone: 'error',
        titleKey: 'backupDr.restoreVerification.realPipeline.listFailedUnknownTitle',
        bodyKey: 'backupDr.restoreVerification.realPipeline.listFailedUnknownBody',
      };
  }
}
