/**
 * Manuel yedek / restore tatbikatı tetikleri — etkin çalıştırma modu ve son çalıştırma adaptörüne göre onay metinleri.
 */
import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import { isSimulatedBackupAdapterKind } from '@/features/backup-dr/logic/backupDrMappers';

export interface LatestRunSnapshot {
  id?: string | null;
  adapterKind?: string | null;
  isSimulatedExecution?: boolean | null;
}

export interface ManualActionsPresentationOptions {
  /** execution-mode API yüklenmediyse yedek sağlık özetindeki etkin adaptör (bilgi satırı). */
  healthEffectiveAdapterKind?: string | null;
}

export interface ManualActionsModeConfirmations {
  /** Tetik düğmelerinin üstünde — etkin mod; null ise satır gösterilmez */
  actionBannerLine: string | null;
  backupTitle: string;
  backupDescriptionParts: string[];
  restoreTitle: string;
  restoreDescriptionParts: string[];
  /** Kart üstünde istenen vs etkin uyumsuzluğu / blokaj */
  cardAlert: { severity: 'warning' | 'error'; message: string } | null;
}

function labelUserFacing(
  mode: string | undefined,
  t: (k: string, o?: Record<string, string | number>) => string
): string {
  const m = (mode ?? '').trim();
  if (m === 'Fake') return t('backupDr.executionMode.userFacing.fake');
  if (m === 'RealPgDump') return t('backupDr.executionMode.userFacing.realPgDump');
  if (m === 'UseConfigurationDefault') return t('backupDr.executionMode.userFacing.useConfig');
  if (m === 'ProductionStub') return t('backupDr.executionMode.userFacing.productionStub');
  return m || t('backupDr.manual.modeLabelUnknown');
}

function classifyLatestRunForDrill(
  latest: LatestRunSnapshot | null | undefined
): 'none' | 'simulated' | 'real' | 'unknown' {
  if (!latest?.id) return 'none';
  const k = (latest.adapterKind ?? '').trim();
  if (latest.isSimulatedExecution === true || isSimulatedBackupAdapterKind(k)) return 'simulated';
  if (k === 'PgDump') return 'real';
  return 'unknown';
}

/**
 * Popconfirm / kart için parçalı metinler ve tetik öncesi uyarı üretir.
 */
export function buildManualActionsConfirmations(
  em: BackupExecutionModeTruth,
  latestRun: LatestRunSnapshot | null | undefined,
  t: (k: string, o?: Record<string, string | number>) => string,
  options?: ManualActionsPresentationOptions
): ManualActionsModeConfirmations {
  const healthAdapter = (options?.healthEffectiveAdapterKind ?? '').trim();
  const effectiveUf = labelUserFacing(em.loaded ? em.effectiveUserFacingMode : undefined, t);
  const requestedUf = labelUserFacing(em.loaded ? em.requestedUserFacingMode : undefined, t);
  const effAdapter = em.loaded
    ? (em.effectiveExecutionAdapterKind || '—').trim()
    : healthAdapter || '—';

  const effectiveLine = em.loaded
    ? t('backupDr.manual.effectiveExecutionLine', {
        mode: effectiveUf,
        adapter: effAdapter || '—',
      })
    : t('backupDr.manual.effectiveExecutionLineFallback', { adapter: healthAdapter || '—' });

  const backupParts: string[] = [t('backupDr.manual.confirmBackupIntro')];

  backupParts.push(effectiveLine);

  if (em.loaded && em.requestedRealButEffectiveSimulated) {
    backupParts.push(
      t('backupDr.manual.confirmBackupMismatchRequestedRealEffectiveSimulated', {
        requested: requestedUf,
        effective: effectiveUf,
      })
    );
  }
  if (em.loaded && em.requestedRealButBlocked) {
    backupParts.push(t('backupDr.manual.confirmBackupMismatchRequestedRealBlocked'));
  }

  if (em.loaded && em.effectiveIsSimulatedAdapter) {
    backupParts.push(t('backupDr.manual.confirmBackupBehaviorSimulated'));
  } else if (em.loaded && em.effectiveIsPgDumpAdapter) {
    backupParts.push(t('backupDr.manual.confirmBackupBehaviorRealPgDump'));
  } else if (em.loaded) {
    backupParts.push(
      t('backupDr.manual.confirmBackupBehaviorOtherAdapter', { adapter: effAdapter || '—' })
    );
  } else {
    backupParts.push(t('backupDr.manual.confirmBackupBehaviorUnknown'));
  }

  const restoreParts: string[] = [t('backupDr.manual.confirmRestoreIntro')];

  const drill = classifyLatestRunForDrill(latestRun);
  if (drill === 'none') {
    restoreParts.push(t('backupDr.manual.confirmRestoreLatestRunUnknown'));
  } else if (drill === 'simulated') {
    restoreParts.push(
      t('backupDr.manual.confirmRestoreLatestSimulated', {
        adapter: (latestRun?.adapterKind ?? '—').trim() || '—',
      })
    );
  } else if (drill === 'real') {
    restoreParts.push(t('backupDr.manual.confirmRestoreLatestReal'));
  } else {
    restoreParts.push(
      t('backupDr.manual.confirmRestoreLatestAmbiguous', {
        adapter: (latestRun?.adapterKind ?? '—').trim() || '—',
      })
    );
  }

  restoreParts.push(t('backupDr.manual.confirmRestoreDrillFootnote'));

  let cardAlert: ManualActionsModeConfirmations['cardAlert'] = null;
  if (em.loaded && em.requestedRealButEffectiveSimulated) {
    cardAlert = {
      severity: 'error',
      message: t('backupDr.manual.cardAlertRequestedRealEffectiveSimulated', {
        requested: requestedUf,
        effective: effectiveUf,
      }),
    };
  } else if (em.loaded && em.requestedRealButBlocked) {
    cardAlert = {
      severity: 'warning',
      message: t('backupDr.manual.cardAlertRequestedRealBlocked'),
    };
  }

  const actionBannerLine = em.loaded
    ? t('backupDr.manual.actionBannerEffective', { mode: effectiveUf, adapter: effAdapter || '—' })
    : healthAdapter
      ? t('backupDr.manual.actionBannerAdapterOnly', { adapter: healthAdapter })
      : null;

  return {
    actionBannerLine,
    backupTitle: t('backupDr.manual.confirmBackupTitle'),
    backupDescriptionParts: backupParts,
    restoreTitle: t('backupDr.manual.confirmRestoreTitle'),
    restoreDescriptionParts: restoreParts,
    cardAlert,
  };
}
