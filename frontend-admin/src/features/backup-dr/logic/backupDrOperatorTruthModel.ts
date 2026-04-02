/**
 * Merkezi operatör-doğruluk görünüm modeli: çalıştırma, kurtarılabilirlik, artefakt, restore ve uyarı yüzeyi tek kaynaktan türetilir.
 * Bileşenler başarı/yeşil anlamını kendi başına yeniden yorumlamaz; bu dosyadaki alanları kullanır.
 */

import type {
  BackupConfigurationHealthResponseDto,
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreCapabilityDto,
  RestoreVerificationReadinessResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  BackupRunResponseDtoStatus,
  BackupVerificationResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import {
  computeEffectiveRestoreReadinessLevel,
  configurationHealthSummaryI18nKey,
  externalCopyVariantToI18nKey,
  isSimulatedBackupAdapterKind,
  mapConfigurationHealthLevel,
  normalizeHealthLevelString,
  type ConfigurationHealthUiKind,
  type ExternalCopyVariant,
} from '@/features/backup-dr/logic/backupDrMappers';
import type { BackupEvidenceLadderModel } from '@/features/backup-dr/logic/backupDrEvidenceLadder';
import { deriveBackupEvidenceLadder } from '@/features/backup-dr/logic/backupDrEvidenceLadder';
import {
  PG_RESTORE_LIST_FAILED,
  interpretPgRestoreListFailure,
  pgRestoreListFailureKindToBannerMessageKey,
} from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';
import {
  deriveBackupExecutionModeTruth,
  type BackupExecutionModeTruth,
} from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import {
  buildManualActionsConfirmations,
  type ManualActionsModeConfirmations,
} from '@/features/backup-dr/logic/backupManualActionsModePresentation';
import {
  buildBackupOperatorTruthProvenance,
  type BackupOperatorTruthProvenance,
  type BackupTruthContractHints,
} from '@/features/backup-dr/logic/backupDrTruthExtensionPoints';

export type { BackupOperatorTruthProvenance, BackupTruthContractHints } from '@/features/backup-dr/logic/backupDrTruthExtensionPoints';

export type OperatorTruthTranslate = (key: string, options?: Record<string, string | number>) => string;

export type { BackupExecutionModeTruth };

function restoreDrillSimulatedHeuristic(
  health: BackupConfigurationHealthResponseDto | undefined,
  latest: BackupRunResponseDto | undefined,
  detailForPipeline: BackupRunResponseDto | null | undefined,
  executionMode: BackupExecutionModeTruth,
): boolean {
  if (executionMode.loaded && executionMode.effectiveIsSimulatedAdapter) return true;
  return (
    isSimulatedBackupAdapterKind(health?.effectiveAdapterKind) ||
    isSimulatedBackupAdapterKind(latest?.adapterKind) ||
    detailForPipeline?.isSimulatedExecution === true
  );
}

export interface AutomatedRestoreCapabilityModel {
  /** Ham API alanı — UI yalnızca rapor eder (backendReportedCapability). */
  raw: boolean | undefined;
  labelKey: string;
}

export function automatedRestoreCapabilityFromStatus(
  restore: RestoreCapabilityDto | undefined,
): AutomatedRestoreCapabilityModel {
  const raw = restore?.isAutomatedRestoreAvailable;
  return {
    raw,
    labelKey:
      raw === true
        ? 'backupDr.restoreCapability.reportedEnabled'
        : raw === false
          ? 'backupDr.restoreCapability.reportedDisabled'
          : 'backupDr.restoreCapability.reportedUnknown',
  };
}

export function hasRecoverabilityProofGaps(summary: BackupRecoverabilitySummaryResponseDto | undefined): boolean {
  if (!summary) return true;
  const noBackupProof = !summary.lastSuccessfulBackupAt;
  const noArtifactProof = !summary.lastSuccessfulArtifactVerificationAt;
  const noRestoreProof = !summary.lastSuccessfulRestoreProofAt;
  return noBackupProof || noArtifactProof || noRestoreProof;
}

/**
 * API sinyallerine göre yedek “veri düzlemi”: UI gerçek PostgreSQL dökümü ile stub’ı ayırır.
 */
export type BackupDataPlane =
  | 'simulated_or_fake'
  | 'config_no_real_pg_dump'
  | 'config_unknown_real_pg'
  | 'operational_pg_dump_configured';

/** API çalıştırma durumu: teknik tamamlanma (kurtarılabilirlik değil). */
export interface RunTruth {
  /** `BackupRunResponseDto.status === Succeeded` — operasyonel / teknik tamamlanma. */
  technicalSuccess: boolean;
  /** Üretim pg_dump kanıtı değil (Fake/Stub veya API simülasyon bayrağı). */
  simulatedEvidence: boolean;
  /** Simülasyon bilgisi API bayrağından mı, adaptör adı çıkarımından mı. */
  simulatedEvidenceSource: 'api_flag' | 'adapter_inference' | 'none';
  /**
   * Teknik başarı veya yapılandırma, kurtarılabilirlik kanıtı anlamına gelmez:
   * simüle çalıştırma, başarısız/askıda çalıştırma veya pg_dump yapılandırması yok.
   */
  recoverabilityNotProven: boolean;
  latestRunId: string | undefined;
  /** `BackupConfigurationHealth` / recoverability özeti birleşimi — ikisi de false değilse true. */
  realPostgreSqlLogicalDumpConfigured: boolean | undefined;
  /** Stub/simüle mi, gerçek pg_dump yolu yok mu, bilinmiyor mu, yol açık mı. */
  dataPlane: BackupDataPlane;
}

/** Recoverability-summary satırı. */
export interface RecoverabilityTruth {
  recoverabilityNotProven: boolean;
  hasProofGaps: boolean;
}

/** Harici kopya + global artefakt doğrulama kapsamı. */
export interface ArtifactTruth {
  externalCopyVariant: ExternalCopyVariant;
  /** i18n ile çözülmüş özet satırı (istatistik kartı). */
  externalCopyDisplayText: string;
  /** Lifecycle birleştirmesi istemci tarafında. */
  externalArchiveTruthKind: 'frontend_inferred';
  /** `GET .../verification/latest` ile `latestRun.id` ilişkisi. */
  globalVerificationScope: 'matches_latest_run' | 'mismatch' | 'unlinked' | 'no_verification' | 'no_latest_run';
  globalVerificationBackupRunId: string | undefined;
  /**
   * execution-mode yüklüyse: worker etkin yüzeyi simüle (Fake/Stub) mi hedefliyor.
   * İndirme kartları “gerçek döküm yok” ile hizalanır.
   */
  backupWorkerTargetsSimulatedSurface: boolean | undefined;
}

/** Restore hazırlığı + otomatik geri yükleme bayrağı + son drill. */
export interface RestoreTruth {
  apiReadinessLevel: string | undefined | null;
  effectiveReadinessLevel: string | undefined | null;
  /** `computeEffectiveRestoreReadinessLevel` API seviyesini düşürdü mü. */
  readinessCapped: boolean;
  backendReportedCapability: AutomatedRestoreCapabilityModel;
  /** `isAutomatedRestoreAvailable` API’de yok — otomatik geri yükleme bilinmez. */
  policyUnknown: boolean;
  /** Son drill satırı başarısız (API status 3 = failed enum). */
  latestDrillFailed: boolean;
  /** Son drill tamamlandı (API status 2) — pg_restore --list vb. geçti. */
  latestDrillSucceeded: boolean;
}

export interface BannerOperatorTruth {
  critical: string[];
  warn: string[];
  /** Beklenen Fake/stub davranışı — üretim olayı gibi sunulmaz (HealthBanner’da info). */
  info: string[];
}

export type OperatorAlertSeverity = 'error' | 'warning' | 'info';

export interface OperatorAlertRow {
  severity: OperatorAlertSeverity;
  text: string;
  /**
   * Health/restore API issue satırları dışında, üst HealthBanner’da zaten gösterilen sinyaller —
   * Alerts kartında tekrar etmeyi keser (tam metin sorun listelerinde kalır).
   */
  redundantWithBanner?: boolean;
}

/** Pano üstü: tek blokta “bu ortamda yedek gerçekten ne anlama geliyor”. */
export interface OperatorValidityStrip {
  severity: 'warning' | 'info' | 'success';
  titleKey: string;
  descriptionKey: string;
}

export interface BackupOperatorTruthModel {
  run: RunTruth;
  recoverability: RecoverabilityTruth;
  artifact: ArtifactTruth;
  restore: RestoreTruth;
  labels: {
    backupStatus: (status: number | undefined) => string;
    restoreStatus: (status: number | undefined) => string;
  };
  lockHints: {
    backup: string[];
    restore: string[];
  };
  summaryPresentation: {
    summaryBackupFootnoteKey: string;
    summaryRestoreFootnoteKey: string;
    showRealPgDumpOperationalBanner: boolean;
    showDevRealDumpGuidance: boolean;
    /** Özet istatistik değeri: `t(backupHealthSummaryLabelKey)` ile üretilir. */
    backupHealthSummaryLabelKey: string;
    /** Özet istatistik değeri: `t(restoreReadinessSummaryLabelKey)` ile üretilir. */
    restoreReadinessSummaryLabelKey: string;
    backupHealthUiKind: ConfigurationHealthUiKind;
    restoreReadinessUiKind: ConfigurationHealthUiKind;
    /** Konfig kısa satırı (çeviri uygulanmış). */
    configShortSummaryLine: string;
  };
  /** İlerleme bandı için tek kaynaklı boolean’lar (dashboard OR birleşimini tekrarlamaz). */
  progressRunBanner: {
    recoverabilityNotProvenGlance: boolean;
    latestRestoreDrillFailed: boolean;
  };
  manualActionsModeConfirmations: ManualActionsModeConfirmations;
  /** Gercek/Fake operasyonel yuzeyin merkezi karari (dashboard heuristik dagilimini azaltir). */
  simulatedOperationalMode: boolean;
  /** Admin seçilebilir çalıştırma modu: requested / effective / runnable / blokaj tek yerde. */
  executionMode: BackupExecutionModeTruth;
  banner: BannerOperatorTruth;
  /** Health/restore issue tam metinleri + info; HealthBanner ile çakışan satırlar elenir. */
  alerts: OperatorAlertRow[];
  /** Veri düzlemi + kanıt özeti — Fake stub ile gerçek pg_dump yolunu ayırır. */
  operatorValidity: OperatorValidityStrip | null;
  /** Teknik başarı → gerçek döküm → liste tatbikatı → kanıt zaman damgaları basamakları. */
  evidenceLadder: BackupEvidenceLadderModel;
  /**
   * Hangi alt sistemlerin hâlâ sezgisel/çıkarımsal olduğunu özetler (backend sözleşmesi genişletmesi için).
   * Davranışı değiştirmez; telemetri / dokümantasyon / ileride `truthContractHints` ile birleştirmeye uygun.
   */
  truthProvenance: BackupOperatorTruthProvenance;
  /**
   * İleride OpenAPI’ye eklenecek açık alanlar — şu an isteğe bağlı ve yok sayılır.
   * @see BackupTruthContractHints
   */
  truthContractHints?: BackupTruthContractHints;
}

export interface BuildBackupOperatorTruthModelParams {
  t: OperatorTruthTranslate;
  health: BackupConfigurationHealthResponseDto | undefined;
  healthLv: string;
  restoreReady: RestoreVerificationReadinessResponseDto | undefined;
  restoreLv: string;
  latest: BackupRunResponseDto | undefined;
  detailForPipeline: BackupRunResponseDto | null | undefined;
  verification: BackupVerificationResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  recoverabilitySummary: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreCapability: RestoreCapabilityDto | undefined;
  externalCopyVariant: ExternalCopyVariant;
  /**
   * Sayfa aynı API issue metinlerini ayrı kartlarda listeliyorsa (ör. Backup & DR panosu),
   * Alerts akışında tekrarlama — banner + özel kartlar gerçek sorunları gizlemez.
   */
  omitDedicatedSectionIssueDuplicates?: boolean;
  /**
   * GET /api/admin/backup/execution-mode gövdesi; requested / effective / runnable / blokaj türetilir.
   */
  executionModeDto?: BackupExecutionModeResponseDto | null;
  hasStatusPayload?: boolean;
  /**
   * Gelecek backend alanları için uyumluluk yuvası — şu an `buildBackupOperatorTruthModel` içinde kullanılmaz.
   */
  truthContractHints?: BackupTruthContractHints;
}

function labelFromStatusNamespace(
  ns: 'backupStatus' | 'restoreStatus',
  status: number | undefined,
  t: OperatorTruthTranslate,
): string {
  const s = status ?? 0;
  const key = `backupDr.${ns}.${s}`;
  const label = t(key);
  return label === key ? String(s) : label;
}

function lockHintIssues(issues: string[] | undefined): string[] {
  const re = /lock|advisory|distributed|kilit|sperre/i;
  return (issues ?? []).filter((x) => re.test(x));
}

export function deriveRunTruth(
  latest: BackupRunResponseDto | undefined,
  detailForPipeline: BackupRunResponseDto | null | undefined,
  health: BackupConfigurationHealthResponseDto | undefined,
  recoverabilitySummary: BackupRecoverabilitySummaryResponseDto | undefined,
): RunTruth {
  const technicalSuccess = latest?.status === BackupRunResponseDtoStatus.NUMBER_3;

  let simulatedEvidence = false;
  let simulatedEvidenceSource: RunTruth['simulatedEvidenceSource'] = 'none';
  if (detailForPipeline?.isSimulatedExecution === true || latest?.isSimulatedExecution === true) {
    simulatedEvidence = true;
    simulatedEvidenceSource = 'api_flag';
  } else if (isSimulatedBackupAdapterKind(latest?.adapterKind)) {
    simulatedEvidence = true;
    simulatedEvidenceSource = 'adapter_inference';
  }

  const noRealPgConfigured =
    health?.realPostgreSqlLogicalDumpConfigured === false ||
    recoverabilitySummary?.realPostgreSqlLogicalDumpConfigured === false;

  const recoverabilityNotProven =
    !technicalSuccess || simulatedEvidence || noRealPgConfigured === true;

  const pgHealth = health?.realPostgreSqlLogicalDumpConfigured;
  const pgRec = recoverabilitySummary?.realPostgreSqlLogicalDumpConfigured;
  let realPostgreSqlLogicalDumpConfigured: boolean | undefined;
  if (pgHealth === true || pgRec === true) realPostgreSqlLogicalDumpConfigured = true;
  else if (pgHealth === false || pgRec === false) realPostgreSqlLogicalDumpConfigured = false;

  let dataPlane: BackupDataPlane;
  if (simulatedEvidence) dataPlane = 'simulated_or_fake';
  else if (realPostgreSqlLogicalDumpConfigured === false) dataPlane = 'config_no_real_pg_dump';
  else if (realPostgreSqlLogicalDumpConfigured === true) dataPlane = 'operational_pg_dump_configured';
  else dataPlane = 'config_unknown_real_pg';

  return {
    technicalSuccess,
    simulatedEvidence,
    simulatedEvidenceSource,
    recoverabilityNotProven,
    latestRunId: latest?.id,
    realPostgreSqlLogicalDumpConfigured,
    dataPlane,
  };
}

export function deriveRecoverabilityTruth(summary: BackupRecoverabilitySummaryResponseDto | undefined): RecoverabilityTruth {
  const hasProofGaps = hasRecoverabilityProofGaps(summary);
  return {
    recoverabilityNotProven: hasProofGaps || !summary,
    hasProofGaps,
  };
}

export function deriveArtifactTruth(
  verification: BackupVerificationResponseDto | undefined,
  latestRunId: string | undefined,
  externalCopyVariant: ExternalCopyVariant,
  t: OperatorTruthTranslate,
): ArtifactTruth {
  let globalVerificationScope: ArtifactTruth['globalVerificationScope'] = 'no_verification';
  if (!latestRunId) globalVerificationScope = 'no_latest_run';
  else if (!verification) globalVerificationScope = 'no_verification';
  else {
    const bid = verification.backupRunId?.trim();
    if (!bid) globalVerificationScope = 'unlinked';
    else if (bid === latestRunId) globalVerificationScope = 'matches_latest_run';
    else globalVerificationScope = 'mismatch';
  }

  return {
    externalCopyVariant,
    externalCopyDisplayText: t(externalCopyVariantToI18nKey(externalCopyVariant)),
    externalArchiveTruthKind: 'frontend_inferred',
    globalVerificationScope,
    globalVerificationBackupRunId: verification?.backupRunId,
    backupWorkerTargetsSimulatedSurface: undefined,
  };
}

export function deriveRestoreTruth(params: {
  restoreReady: RestoreVerificationReadinessResponseDto | undefined;
  effectiveReadinessLevel: string | undefined | null;
  restoreCapability: RestoreCapabilityDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
}): RestoreTruth {
  const apiReadinessLevel = params.restoreReady?.level;
  const readinessCapped =
    normalizeHealthLevelString(apiReadinessLevel) !== normalizeHealthLevelString(params.effectiveReadinessLevel ?? '');
  const cap = automatedRestoreCapabilityFromStatus(params.restoreCapability);
  const st = params.restoreLatest?.status;
  return {
    apiReadinessLevel,
    effectiveReadinessLevel: params.effectiveReadinessLevel,
    readinessCapped,
    backendReportedCapability: cap,
    policyUnknown: cap.raw === undefined,
    latestDrillFailed: st === RestoreVerificationRunResponseDtoStatus.NUMBER_3,
    latestDrillSucceeded: st === RestoreVerificationRunResponseDtoStatus.NUMBER_2,
    backupExecutionProfileRunnable: undefined,
  };
}

/**
 * Kurtarılabilirlik kanıtı + gerçek pg_dump yolu + son drill — operatör için tek özet şerit.
 */
export function deriveOperatorValidityStrip(params: {
  run: RunTruth;
  recoverability: RecoverabilityTruth;
  restore: RestoreTruth;
  executionMode: BackupExecutionModeTruth;
}): OperatorValidityStrip | null {
  const { run, recoverability, restore, executionMode } = params;
  const em = executionMode;

  if (em.loaded && em.requestedRealButEffectiveSimulated) {
    return {
      severity: 'warning',
      titleKey: 'backupDr.operatorValidity.executionModeRequestedRealEffectiveSimulatedTitle',
      descriptionKey: 'backupDr.operatorValidity.executionModeRequestedRealEffectiveSimulatedBody',
    };
  }

  if (em.loaded && em.requestedFakeButEffectivePgDump) {
    return {
      severity: 'warning',
      titleKey: 'backupDr.operatorValidity.executionModeRequestedFakeEffectivePgDumpTitle',
      descriptionKey: 'backupDr.operatorValidity.executionModeRequestedFakeEffectivePgDumpBody',
    };
  }

  if (em.loaded && em.requestedRealButBlocked) {
    return {
      severity: 'warning',
      titleKey: 'backupDr.operatorValidity.executionModeRequestedRealBlockedTitle',
      descriptionKey: 'backupDr.operatorValidity.executionModeRequestedRealBlockedBody',
    };
  }

  if (em.loaded && em.effectiveIsSimulatedAdapter) {
    return {
      severity: 'info',
      titleKey: 'backupDr.operatorValidity.stubDataPlaneTitle',
      descriptionKey: 'backupDr.operatorValidity.executionModeFakeOverrideBody',
    };
  }

  if (run.dataPlane === 'simulated_or_fake') {
    return {
      severity: 'info',
      titleKey: 'backupDr.operatorValidity.stubDataPlaneTitle',
      descriptionKey: 'backupDr.operatorValidity.stubDataPlaneBody',
    };
  }
  if (run.dataPlane === 'config_no_real_pg_dump') {
    return {
      severity: 'warning',
      titleKey: 'backupDr.operatorValidity.noRealPgConfiguredTitle',
      descriptionKey: 'backupDr.operatorValidity.noRealPgConfiguredBody',
    };
  }
  if (run.dataPlane === 'config_unknown_real_pg') {
    return {
      severity: 'info',
      titleKey: 'backupDr.operatorValidity.realPgUnknownTitle',
      descriptionKey: 'backupDr.operatorValidity.realPgUnknownBody',
    };
  }

  if (recoverability.recoverabilityNotProven) {
    return {
      severity: 'info',
      titleKey: 'backupDr.operatorValidity.realPgButProofGapsTitle',
      descriptionKey: 'backupDr.operatorValidity.realPgButProofGapsBody',
    };
  }
  if (restore.latestDrillFailed) {
    return {
      severity: 'warning',
      titleKey: 'backupDr.operatorValidity.latestDrillFailedTitle',
      descriptionKey: 'backupDr.operatorValidity.latestDrillFailedBody',
    };
  }
  if (restore.latestDrillSucceeded) {
    return {
      severity: 'success',
      titleKey: 'backupDr.operatorValidity.strongSignalsTitle',
      descriptionKey: 'backupDr.operatorValidity.strongSignalsBody',
    };
  }
  return {
    severity: 'info',
    titleKey: 'backupDr.operatorValidity.realPgOperationalTitle',
    descriptionKey: 'backupDr.operatorValidity.realPgOperationalBody',
  };
}

/** Konfigürasyon sağlığı etiketi: bilinmeyen seviye yeşil gösterilmez. */
export function tagColorForConfigurationHealthUiKind(kind: ConfigurationHealthUiKind): 'red' | 'orange' | 'green' | 'default' {
  if (kind === 'unhealthy') return 'red';
  if (kind === 'degraded') return 'orange';
  if (kind === 'healthy') return 'green';
  return 'default';
}

function pushBannerFromAlerts(
  t: OperatorTruthTranslate,
  health: BackupConfigurationHealthResponseDto | undefined,
  healthLv: string,
  restoreReady: RestoreVerificationReadinessResponseDto | undefined,
  restoreLv: string,
  latest: BackupRunResponseDto | undefined,
  detailForPipeline: BackupRunResponseDto | null | undefined,
  verification: BackupVerificationResponseDto | undefined,
  externalCopyVariant: ExternalCopyVariant,
  restoreLatest: RestoreVerificationRunResponseDto | undefined,
  executionMode: BackupExecutionModeTruth,
): BannerOperatorTruth {
  const critical: string[] = [];
  const warn: string[] = [];
  const info: string[] = [];

  if (executionMode.loaded && executionMode.requestedRealButEffectiveSimulated) {
    critical.push(t('backupDr.banner.executionModeRequestedRealEffectiveSimulated'));
  }
  if (executionMode.loaded && executionMode.requestedRealButBlocked) {
    warn.push(t('backupDr.banner.executionModeRequestedRealBlocked'));
  }
  if (executionMode.loaded && executionMode.requestedFakeButEffectivePgDump) {
    warn.push(t('backupDr.banner.executionModeRequestedFakeEffectivePgDump'));
  }
  if (executionMode.loaded && executionMode.fallbackBehavior === 'operator_guidance_only') {
    info.push(
      t('backupDr.banner.executionModeFallbackGuidanceOnly', {
        mode: executionMode.recommendedFallbackUserFacingMode ?? '',
      }),
    );
  }

  if (healthLv === 'unhealthy' && !(health?.issues?.length)) critical.push(t('backupDr.banner.backupConfigUnhealthy'));
  else if (healthLv === 'degraded' && !(health?.issues?.length)) warn.push(t('backupDr.banner.backupConfigDegraded'));

  if (restoreLv === 'unhealthy' && !(restoreReady?.issues?.length))
    critical.push(t('backupDr.banner.restoreReadinessUnhealthy'));
  else if (restoreLv === 'degraded' && !(restoreReady?.issues?.length))
    warn.push(t('backupDr.banner.restoreReadinessDegraded'));

  if (health && !health.workerEnabled) critical.push(t('backupDr.health.workerDisabled'));

  if (health && health.realPostgreSqlLogicalDumpConfigured === false) {
    const n = health.readinessNarrative?.trim();
    warn.push(n ? `${t('backupDr.banner.noRealPostgreSqlBackup')}: ${n}` : t('backupDr.banner.noRealPostgreSqlBackup'));
  }

  if (restoreReady && !restoreReady.workerEnabled) warn.push(t('backupDr.readiness.restoreWorkerDisabled'));

  if (restoreReady?.orchestratorDistributedLockEnabled === false) warn.push(t('backupDr.lock.restoreLockDisabled'));

  const lr = latest;
  if (lr?.status === 4 || lr?.status === 5) {
    critical.push(
      `${t('backupDr.latestRun.failure')}: ${lr.failureCode ?? '—'} — ${(lr.failureDetail ?? '').trim()}`.trim(),
    );
  }

  const v = verification;
  if (v && v.status === BackupVerificationResponseDtoStatus.NUMBER_2 && v.failureReason) {
    warn.push(`${t('backupDr.artifactVerification.failed')}: ${v.failureReason}`);
  }

  if (externalCopyVariant === 'failed' || externalCopyVariant === 'mixed') {
    warn.push(t('backupDr.banner.externalArchiveDegraded'));
  }

  const latestSucceededSimulated =
    lr?.status === BackupRunResponseDtoStatus.NUMBER_3 &&
    (detailForPipeline?.isSimulatedExecution === true || isSimulatedBackupAdapterKind(lr?.adapterKind));
  /** recoverability block + noRealPostgreSql line already explain “not production pg_dump”; avoid repeating. */
  const skipSimulatedBanner =
    health?.realPostgreSqlLogicalDumpConfigured === false && latestSucceededSimulated;
  if (latestSucceededSimulated && !skipSimulatedBanner) {
    info.push(t('backupDr.banner.latestRunSimulatedNotProduction'));
  }

  const rr = restoreLatest;
  if (rr && rr.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3) {
    const simHeuristic = restoreDrillSimulatedHeuristic(health, latest, detailForPipeline, executionMode);
    if (rr.failureCode === PG_RESTORE_LIST_FAILED) {
      const interp = interpretPgRestoreListFailure({ run: rr, isSimulatedPipelineHeuristic: simHeuristic });
      if (interp) {
        const { tier, key } = pgRestoreListFailureKindToBannerMessageKey(interp.kind);
        if (tier === 'info') info.push(t(key));
        else if (tier === 'warn') warn.push(t(key));
        else critical.push(t(key));
      }
    } else {
      critical.push(
        `${t('backupDr.restoreVerification.drillFailed')}: ${rr.failureCode ?? ''} ${(rr.failureDetail ?? '').trim()}`.trim(),
      );
    }
  }

  const healthIssueCount = health?.issues?.length ?? 0;
  if (healthIssueCount > 0) {
    const line = t('backupDr.banner.healthIssuesSummary', { count: healthIssueCount });
    if (healthLv === 'unhealthy' && !critical.includes(line)) critical.push(line);
    else if (healthLv === 'degraded' && !warn.includes(line)) warn.push(line);
  }

  const restoreIssueCount = restoreReady?.issues?.length ?? 0;
  if (restoreIssueCount > 0) {
    const line = t('backupDr.banner.restoreIssuesSummary', { count: restoreIssueCount });
    if (restoreLv === 'unhealthy' && !critical.includes(line)) critical.push(line);
    else if (restoreLv === 'degraded' && !warn.includes(line)) warn.push(line);
  }

  return { critical, warn, info };
}

function buildOperatorAlertRows(
  t: OperatorTruthTranslate,
  health: BackupConfigurationHealthResponseDto | undefined,
  healthLv: string,
  restoreReady: RestoreVerificationReadinessResponseDto | undefined,
  restoreLv: string,
  latest: BackupRunResponseDto | undefined,
  detailForPipeline: BackupRunResponseDto | null | undefined,
  verification: BackupVerificationResponseDto | undefined,
  restoreLatest: RestoreVerificationRunResponseDto | undefined,
  restoreNotes: string | undefined,
  omitDedicatedSectionIssueDuplicates: boolean | undefined,
  executionMode: BackupExecutionModeTruth,
): OperatorAlertRow[] {
  const items: OperatorAlertRow[] = [];
  if (!omitDedicatedSectionIssueDuplicates) {
    for (const issue of health?.issues ?? []) {
      items.push({
        severity: healthLv === 'unhealthy' ? 'error' : 'warning',
        text: issue,
      });
    }
  }
  if (health && !health.workerEnabled) {
    items.push({
      severity: 'error',
      text: t('backupDr.health.workerDisabled'),
      redundantWithBanner: true,
    });
  }
  if (health && health.realPostgreSqlLogicalDumpConfigured === false) {
    const n = health.readinessNarrative?.trim();
    items.push({
      severity: 'warning',
      text: n ? `${t('backupDr.banner.noRealPostgreSqlBackup')}: ${n}` : t('backupDr.banner.noRealPostgreSqlBackup'),
      redundantWithBanner: true,
    });
  }
  const lr = latest;
  if (lr?.status === 4 || lr?.status === 5) {
    items.push({
      severity: 'error',
      text: `${t('backupDr.latestRun.failure')}: ${lr.failureCode ?? '—'} — ${lr.failureDetail ?? ''}`.trim(),
      redundantWithBanner: true,
    });
  }
  const v = verification;
  if (v && v.status === BackupVerificationResponseDtoStatus.NUMBER_2 && v.failureReason) {
    items.push({
      severity: 'warning',
      text: `${t('backupDr.artifactVerification.failed')}: ${v.failureReason}`,
      redundantWithBanner: true,
    });
  }
  const rr = restoreLatest;
  /** Drill başarısız: üst HealthBanner + RestoreVerificationCard — Fake stub PG_RESTORE_LIST_FAILED Alerts’te yok. */
  if (rr && rr.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3) {
    const simHeuristic = restoreDrillSimulatedHeuristic(health, latest, detailForPipeline, executionMode);
    if (rr.failureCode === PG_RESTORE_LIST_FAILED) {
      const interp = interpretPgRestoreListFailure({ run: rr, isSimulatedPipelineHeuristic: simHeuristic });
      if (interp && interp.kind !== 'fake_stub_expected') {
        const { tier, key } = pgRestoreListFailureKindToBannerMessageKey(interp.kind);
        if (tier === 'warn') {
          items.push({ severity: 'warning', text: t(key), redundantWithBanner: true });
        } else if (tier === 'critical') {
          items.push({ severity: 'error', text: t(key), redundantWithBanner: true });
        }
      }
    } else {
      items.push({
        severity: 'error',
        text: `${t('backupDr.restoreVerification.drillFailedAlert')}: ${rr.failureCode ?? '—'} — ${(rr.failureDetail ?? '').trim() || '—'}`.trim(),
        redundantWithBanner: true,
      });
    }
  }
  if (!omitDedicatedSectionIssueDuplicates) {
    for (const issue of restoreReady?.issues ?? []) {
      items.push({
        severity: restoreLv === 'unhealthy' ? 'error' : 'warning',
        text: issue,
      });
    }
  }
  if (restoreReady && !restoreReady.workerEnabled) {
    items.push({
      severity: 'warning',
      text: t('backupDr.readiness.restoreWorkerDisabled'),
      redundantWithBanner: true,
    });
  }
  if (restoreNotes) items.push({ severity: 'info', text: restoreNotes });
  return items;
}

/**
 * Tüm yedek & DR operatör görünümünü tek seferde üretir (dashboard tek giriş noktası).
 */
export function buildBackupOperatorTruthModel(params: BuildBackupOperatorTruthModelParams & { restoreNotes?: string }): BackupOperatorTruthModel {
  const {
    t,
    health,
    healthLv,
    restoreReady,
    restoreLv,
    latest,
    detailForPipeline,
    verification,
    restoreLatest,
    recoverabilitySummary,
    restoreCapability,
    externalCopyVariant,
    restoreNotes,
    omitDedicatedSectionIssueDuplicates,
    executionModeDto,
    hasStatusPayload,
    truthContractHints,
  } = params;

  const executionMode = deriveBackupExecutionModeTruth(executionModeDto);
  const executionModeUsesSimulatedAdapter = executionMode.loaded && executionMode.effectiveIsSimulatedAdapter;

  const effectiveReadinessLevel = computeEffectiveRestoreReadinessLevel({
    apiLevel: restoreReady?.level,
    realPostgreSqlLogicalDumpConfiguredHealth: health?.realPostgreSqlLogicalDumpConfigured,
    realPostgreSqlLogicalDumpConfiguredRecoverability: recoverabilitySummary?.realPostgreSqlLogicalDumpConfigured,
    latestBackupStatus: latest?.status,
    isLatestRunSimulatedExecution: detailForPipeline?.isSimulatedExecution ?? latest?.isSimulatedExecution,
    latestAdapterKind: latest?.adapterKind,
    executionModeUsesSimulatedAdapter,
  });

  const run = deriveRunTruth(latest, detailForPipeline, health, recoverabilitySummary);
  const recoverability = deriveRecoverabilityTruth(recoverabilitySummary);
  const artifactBase = deriveArtifactTruth(verification, latest?.id, externalCopyVariant, t);
  const artifact: ArtifactTruth = {
    ...artifactBase,
    backupWorkerTargetsSimulatedSurface: executionMode.loaded ? executionMode.effectiveIsSimulatedAdapter : undefined,
  };
  const restoreBase = deriveRestoreTruth({
    restoreReady,
    effectiveReadinessLevel,
    restoreCapability,
    restoreLatest,
  });
  const restore: RestoreTruth = {
    ...restoreBase,
    backupExecutionProfileRunnable: executionMode.loaded ? executionMode.effectiveModeRunnable : undefined,
  };
  const simulatedOperationalMode =
    (executionMode.loaded && executionMode.effectiveIsSimulatedAdapter) ||
    isSimulatedBackupAdapterKind(health?.effectiveAdapterKind) ||
    isSimulatedBackupAdapterKind(detailForPipeline?.adapterKind) ||
    isSimulatedBackupAdapterKind(latest?.adapterKind) ||
    detailForPipeline?.isSimulatedExecution === true ||
    latest?.isSimulatedExecution === true;
  const labels = {
    backupStatus: (status: number | undefined) => labelFromStatusNamespace('backupStatus', status, t),
    restoreStatus: (status: number | undefined) => labelFromStatusNamespace('restoreStatus', status, t),
  };
  const lockHints = {
    backup: lockHintIssues(health?.issues),
    restore: lockHintIssues(restoreReady?.issues),
  };
  const backupHealthUiKind = mapConfigurationHealthLevel(health?.level);
  const restoreReadinessUiKind = mapConfigurationHealthLevel(effectiveReadinessLevel);
  const summaryPresentation = {
    summaryBackupFootnoteKey: simulatedOperationalMode
      ? 'backupDr.summary.backupHealthFootnoteFake'
      : 'backupDr.summary.backupHealthFootnote',
    summaryRestoreFootnoteKey: simulatedOperationalMode
      ? 'backupDr.summary.restoreReadinessFootnoteFake'
      : 'backupDr.summary.restoreReadinessFootnote',
    showRealPgDumpOperationalBanner:
      Boolean(hasStatusPayload) &&
      !simulatedOperationalMode &&
      health?.realPostgreSqlLogicalDumpConfigured === true,
    showDevRealDumpGuidance:
      Boolean(hasStatusPayload) &&
      (simulatedOperationalMode || health?.realPostgreSqlLogicalDumpConfigured !== true),
    backupHealthSummaryLabelKey: configurationHealthSummaryI18nKey(health?.level),
    restoreReadinessSummaryLabelKey: configurationHealthSummaryI18nKey(effectiveReadinessLevel),
    backupHealthUiKind,
    restoreReadinessUiKind,
    configShortSummaryLine: `${t('backupDr.health.adapter')}: ${health?.effectiveAdapterKind ?? '—'} · ${t('backupDr.health.worker')}: ${
      health?.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')
    } · ${
      health?.realPostgreSqlLogicalDumpConfigured
        ? t('backupDr.health.realPgDumpYes')
        : t('backupDr.health.realPgDumpNo')
    }`,
  };
  const progressRunBanner = {
    recoverabilityNotProvenGlance: recoverability.recoverabilityNotProven || run.recoverabilityNotProven,
    latestRestoreDrillFailed: restore.latestDrillFailed,
  };
  const manualActionsModeConfirmations = buildManualActionsConfirmations(executionMode, latest, t, {
    healthEffectiveAdapterKind: health?.effectiveAdapterKind,
  });

  const banner = pushBannerFromAlerts(
    t,
    health,
    healthLv,
    restoreReady,
    restoreLv,
    latest,
    detailForPipeline,
    verification,
    externalCopyVariant,
    restoreLatest,
    executionMode,
  );

  const alertsRaw = buildOperatorAlertRows(
    t,
    health,
    healthLv,
    restoreReady,
    restoreLv,
    latest,
    detailForPipeline,
    verification,
    restoreLatest,
    restoreNotes,
    omitDedicatedSectionIssueDuplicates,
    executionMode,
  );
  const alerts = alertsRaw.filter((row) => !row.redundantWithBanner);

  const operatorValidity = deriveOperatorValidityStrip({
    run,
    recoverability,
    restore,
    executionMode,
  });

  const evidenceLadder = deriveBackupEvidenceLadder({
    latest,
    detailForPipeline,
    verification,
    restoreLatest,
    recoverabilitySummary,
    simulatedEvidence: run.simulatedEvidence || executionModeUsesSimulatedAdapter,
    realPostgreSqlLogicalDumpConfigured: run.realPostgreSqlLogicalDumpConfigured,
    executionMode,
  });

  const truthProvenance = buildBackupOperatorTruthProvenance({
    run,
    recoverability,
    artifact,
    restore,
    executionMode,
    readinessCapped: restore.readinessCapped,
  });

  return {
    run,
    recoverability,
    artifact,
    restore,
    labels,
    lockHints,
    summaryPresentation,
    progressRunBanner,
    manualActionsModeConfirmations,
    simulatedOperationalMode,
    executionMode,
    banner,
    alerts,
    operatorValidity,
    evidenceLadder,
    truthProvenance,
    truthContractHints,
  };
}

/** Geri uyumluluk: eski `buildOperatorTruthBanner` imzası. */
export type OperatorTruthBannerModel = BannerOperatorTruth;

export interface BuildOperatorTruthBannerParams {
  t: OperatorTruthTranslate;
  health: BackupConfigurationHealthResponseDto | undefined;
  healthLv: string;
  restoreReady: RestoreVerificationReadinessResponseDto | undefined;
  restoreLv: string;
  latest: BackupRunResponseDto | undefined;
  detailForPipeline: BackupRunResponseDto | null | undefined;
  verification: BackupVerificationResponseDto | undefined;
  externalCopyVariant: ExternalCopyVariant;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
}

export function buildOperatorTruthBanner(params: BuildOperatorTruthBannerParams): OperatorTruthBannerModel {
  return buildBackupOperatorTruthModel({
    t: params.t,
    health: params.health,
    healthLv: params.healthLv,
    restoreReady: params.restoreReady,
    restoreLv: params.restoreLv,
    latest: params.latest,
    detailForPipeline: params.detailForPipeline,
    verification: params.verification,
    restoreLatest: params.restoreLatest,
    recoverabilitySummary: undefined,
    restoreCapability: undefined,
    externalCopyVariant: params.externalCopyVariant,
  }).banner;
}
