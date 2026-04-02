/**
 * Artefakt indirme satırı / çalıştırma bağlamı için operatör-güvenli anlam modeli (DTO + recoverability sinyali).
 */

import type { BackupArtifactResponseDto } from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { isSimulatedBackupAdapterKind } from '@/features/backup-dr/logic/backupDrMappers';

export type SourceExecutionReality = 'simulated_stub' | 'non_simulated' | 'unknown';

/** Bu satırın DR “kanıt” olarak sunulmaması gerektiği durumlar. */
export type RecoverabilityUseKind =
  | 'not_dr_evidence_simulated'
  | 'not_dr_evidence_unverified_adapter'
  | 'possible_operational_artifact'
  | 'unknown_recovery_value';

export type FilePresenceForDownloadKind = 'reported_present' | 'reported_absent' | 'unknown';

export type DownloadEligibility =
  | { state: 'eligible' }
  | { state: 'blocked'; reason: 'no_manage' | 'file_not_on_server' | 'file_presence_unknown' };

/**
 * Fake/simüle dışında, yalnızca DTO sinyalleriyle sezilen şüpheli durumlar (heuristik — backend gerçeği değildir).
 */
export type NonFakeArtifactSuspicion =
  | 'none'
  /** Sunucu dosyayı indirilebilir bildirdiği halde bayt boyutu yok/NaN — metadata tutarsızlığı. */
  | 'metadata_incomplete'
  /** API’de raporlanan boyut 0 — mantıksal döküm için olağan dışı; depolama/akış soruşturması. */
  | 'zero_reported_size'
  /**
   * Mantıksal döküm (enum 0) ve raporlanan boyut çok küçük — gerçek pg_dump için beklenmedik olabilir;
   * küçük test DB’lerinde yanlış pozitif olabilir.
   */
  | 'tiny_reported_logical_dump';

/** Mantıksal döküm satırı için “çok küçük” eşiği (bayt); yalnızca non-simulated + reported_present için. */
export const TINY_LOGICAL_DUMP_SUSPICION_BYTES_THRESHOLD = 1024;

export interface ArtifactDownloadRowTruth {
  artifactId: string | undefined;
  /** i18n anahtarı — “Logical dump” recovery ima etmez. */
  artifactClassLabelKey: string;
  artifactClassLabelFallbackParams?: Record<string, string | number>;
  /**
   * İndirmeden önce okunmalı: dosyanın gerçekte ne içereceği (Fake adaptöründe `fake-bytes-…` ve JSON manifest gibi).
   * `backupDr.download.contentExpect.*`
   */
  contentExpectationKey: string;
  sourceExecutionReality: SourceExecutionReality;
  recoverabilityUse: RecoverabilityUseKind;
  filePresence: FilePresenceForDownloadKind;
  download: DownloadEligibility;
  /** Fake dışı, indirilebilir görünen satırlar için DTO tabanlı şüphe (UI yükseltme). */
  nonFakeSuspicion: NonFakeArtifactSuspicion;
  /** İndirme satırının bütünlük kanıtı olmadığını göstermek için (non-simulated). */
  showIntegrityPrecheckDisclaimer: boolean;
  /** İndirme öncesi düğme — gerçek politikayı yalnızca API doğrular. */
  downloadProvenByApiOnly: true;
}

export interface RunDownloadContext {
  isSimulatedExecutionFlag: boolean | undefined;
  runAdapterKind: string | null | undefined;
  /** Recoverability summary: realPostgreSqlLogicalDumpConfigured */
  realPostgreSqlLogicalDumpConfigured: boolean | null | undefined;
  canManage: boolean;
}

export function inferSourceExecutionReality(ctx: RunDownloadContext): SourceExecutionReality {
  if (ctx.isSimulatedExecutionFlag === true) return 'simulated_stub';
  if (ctx.isSimulatedExecutionFlag === false) return 'non_simulated';
  if (isSimulatedBackupAdapterKind(ctx.runAdapterKind)) return 'simulated_stub';
  if ((ctx.runAdapterKind ?? '').trim() !== '') return 'non_simulated';
  return 'unknown';
}

export function inferRecoverabilityUse(
  source: SourceExecutionReality,
  realPg: boolean | null | undefined,
): RecoverabilityUseKind {
  if (source === 'simulated_stub') return 'not_dr_evidence_simulated';
  if (source === 'unknown') return 'unknown_recovery_value';
  if (realPg === false) return 'not_dr_evidence_unverified_adapter';
  if (realPg === true) return 'possible_operational_artifact';
  return 'unknown_recovery_value';
}

export function inferFilePresenceKind(
  isFilePresentForDownload: boolean | undefined,
): FilePresenceForDownloadKind {
  if (isFilePresentForDownload === true) return 'reported_present';
  if (isFilePresentForDownload === false) return 'reported_absent';
  return 'unknown';
}

/**
 * Simüle/Fake beklenen stub dışında, API metadata’sı şüpheli görünen durumlar.
 * `unknown` kaynakta yalnızca nesnel tutarsızlıklar (boyut yok/0); “küçük döküm” yalnızca non_simulated.
 */
export function inferNonFakeArtifactSuspicion(
  artifact: BackupArtifactResponseDto,
  source: SourceExecutionReality,
): NonFakeArtifactSuspicion {
  if (source === 'simulated_stub') return 'none';
  if (artifact.isFilePresentForDownload !== true) return 'none';

  const raw = artifact.byteSize;
  if (raw == null || (typeof raw === 'number' && Number.isNaN(raw))) {
    return source === 'non_simulated' || source === 'unknown' ? 'metadata_incomplete' : 'none';
  }
  if (raw === 0) {
    return source === 'non_simulated' || source === 'unknown' ? 'zero_reported_size' : 'none';
  }
  if (source !== 'non_simulated') return 'none';
  if (artifact.artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0) {
    if (raw > 0 && raw < TINY_LOGICAL_DUMP_SUSPICION_BYTES_THRESHOLD) return 'tiny_reported_logical_dump';
  }
  return 'none';
}

export function shouldShowIntegrityPrecheckDisclaimer(source: SourceExecutionReality): boolean {
  return source === 'non_simulated' || source === 'unknown';
}

/** i18n: `backupDr.download.suspicion.<kind>.short` / `.detail` — kind `none` için null. */
export function nonFakeSuspicionMessageKeys(
  kind: NonFakeArtifactSuspicion,
): { short: string; detail: string } | null {
  if (kind === 'none') return null;
  return {
    short: `backupDr.download.suspicion.${kind}.short`,
    detail: `backupDr.download.suspicion.${kind}.detail`,
  };
}

/**
 * OpenAPI `artifactType` için güvenli etiket anahtarı — logical dump için ayrı “stub / kanıtlanmamış” metinleri.
 */
export function artifactClassLabelKeyForType(
  artifactType: number | undefined,
  source: SourceExecutionReality,
  realPg: boolean | null | undefined,
): string {
  const t = artifactType ?? -1;
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_0) {
    if (source === 'simulated_stub') return 'backupDr.download.types.logicalDumpStub';
    if (realPg === true && source === 'non_simulated') return 'backupDr.download.types.logicalDumpOperational';
    return 'backupDr.download.types.logicalDumpNotProven';
  }
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_4) {
    if (source === 'simulated_stub') return 'backupDr.download.types.manifestStub';
    return 'backupDr.download.types.4';
  }
  /** Enum 1–3,5: simüle koşuda genel “backup” adları yanıltıcı — ayrı stub anahtarları. */
  if (Number.isInteger(t) && t >= 1 && t <= 5) {
    if (source === 'simulated_stub') return `backupDr.download.types.stub.${t}`;
    return `backupDr.download.types.${t}`;
  }
  return 'backupDr.download.types.unknown';
}

/**
 * Operatörün indirdiğinde göreceği içerik — özellikle Fake adaptörünün `fake-bytes-…` + JSON manifest çifti için.
 */
export function artifactContentExpectationKey(
  artifactType: number | undefined,
  source: SourceExecutionReality,
  realPg: boolean | null | undefined,
): string {
  const t = artifactType ?? -1;
  if (source === 'simulated_stub') {
    if (t === BackupArtifactResponseDtoArtifactType.NUMBER_0) return 'backupDr.download.contentExpect.stubLogicalDumpFakeAdapter';
    if (t === BackupArtifactResponseDtoArtifactType.NUMBER_4) return 'backupDr.download.contentExpect.stubManifestFakeAdapter';
    if (Number.isInteger(t) && t >= 1 && t <= 5) return `backupDr.download.contentExpect.stubTyped.${t}`;
    return 'backupDr.download.contentExpect.stubOther';
  }
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_0) {
    if (realPg === true) return 'backupDr.download.contentExpect.operationalLogicalDump';
    if (realPg === false) return 'backupDr.download.contentExpect.logicalDumpNotProvenConfig';
    return 'backupDr.download.contentExpect.logicalDumpProofUnknown';
  }
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_4) {
    return 'backupDr.download.contentExpect.verificationManifest';
  }
  if (Number.isInteger(t) && t >= 1 && t <= 3) {
    return `backupDr.download.contentExpect.nonSimulatedTyped.${t}`;
  }
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_5) {
    return 'backupDr.download.contentExpect.nonSimulatedTyped.5';
  }
  return 'backupDr.download.contentExpect.unknown';
}

/** Çoklu artefakt tablosunda: mantıksal döküm önce, manifest sonra (Fake çifti okunaklı olsun). */
export function sortArtifactsForOperatorDisplay(artifacts: BackupArtifactResponseDto[]): BackupArtifactResponseDto[] {
  const rank = (artifactType: number | undefined): number => {
    const x = artifactType ?? 999;
    if (x === BackupArtifactResponseDtoArtifactType.NUMBER_0) return 0;
    if (x === BackupArtifactResponseDtoArtifactType.NUMBER_4) return 1;
    if (x >= 1 && x <= 5) return 2 + x;
    return 100 + x;
  };
  return [...artifacts].sort((a, b) => rank(a.artifactType) - rank(b.artifactType));
}

/** Tablo satırında kısa “gerçeklik” rozeti (stub vs gerçek hat). */
export function artifactRealityBadgeKey(source: SourceExecutionReality): string {
  if (source === 'simulated_stub') return 'backupDr.download.reality.stub';
  if (source === 'non_simulated') return 'backupDr.download.reality.realPipeline';
  return 'backupDr.download.reality.unknown';
}

/**
 * Bayt sütunu alt satırı — stub için “küçük = beklenen”; manifest için metadata uyarısı.
 */
export function artifactByteSizeFootnoteKey(
  artifactType: number | undefined,
  source: SourceExecutionReality,
): string | null {
  if (source === 'simulated_stub') return 'backupDr.download.byteSizeFootnote.stubExpectedTiny';
  const t = artifactType ?? -1;
  if (t === BackupArtifactResponseDtoArtifactType.NUMBER_4) {
    return 'backupDr.download.byteSizeFootnote.manifestMetadataOnly';
  }
  return null;
}

/** Tablo hücresi: uzun contentExpect yerine tek satır özet; tam metin Tooltip’te. */
export function contentExpectationTableSummaryKey(
  artifactType: number | undefined,
  source: SourceExecutionReality,
  realPg: boolean | null | undefined,
): string | null {
  if (source === 'simulated_stub') {
    if (artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0) {
      return 'backupDr.download.contentExpectSummary.stubLogicalDumpFakeAdapter';
    }
    if (artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_4) {
      return 'backupDr.download.contentExpectSummary.stubManifestFakeAdapter';
    }
  }
  if (artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_4 && source === 'non_simulated') {
    return 'backupDr.download.contentExpectSummary.manifestNonStub';
  }
  if (artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0 && source === 'non_simulated' && realPg === true) {
    return 'backupDr.download.contentExpectSummary.logicalDumpOperationalShort';
  }
  return null;
}

export function shouldConfirmDownloadUnprovenLogicalDump(
  artifact: BackupArtifactResponseDto,
  truth: ArtifactDownloadRowTruth,
): boolean {
  if (truth.sourceExecutionReality !== 'non_simulated') return false;
  if (artifact.artifactType !== BackupArtifactResponseDtoArtifactType.NUMBER_0) return false;
  return truth.recoverabilityUse === 'not_dr_evidence_unverified_adapter';
}

export function recoverabilityUseShortKey(use: RecoverabilityUseKind): string {
  const m: Record<RecoverabilityUseKind, string> = {
    not_dr_evidence_simulated: 'backupDr.download.recoverabilityUse.short.not_dr_evidence_simulated',
    not_dr_evidence_unverified_adapter: 'backupDr.download.recoverabilityUse.short.not_dr_evidence_unverified_adapter',
    possible_operational_artifact: 'backupDr.download.recoverabilityUse.short.possible_operational_artifact',
    unknown_recovery_value: 'backupDr.download.recoverabilityUse.short.unknown_recovery_value',
  };
  return m[use];
}

export function buildArtifactDownloadRowTruth(
  artifact: BackupArtifactResponseDto,
  ctx: RunDownloadContext,
): ArtifactDownloadRowTruth {
  const source = inferSourceExecutionReality(ctx);
  const recoverabilityUse = inferRecoverabilityUse(source, ctx.realPostgreSqlLogicalDumpConfigured);
  const filePresence = inferFilePresenceKind(artifact.isFilePresentForDownload);

  let download: DownloadEligibility = { state: 'eligible' };
  if (!ctx.canManage) download = { state: 'blocked', reason: 'no_manage' };
  else if (filePresence === 'reported_absent') download = { state: 'blocked', reason: 'file_not_on_server' };
  else if (filePresence === 'unknown') download = { state: 'blocked', reason: 'file_presence_unknown' };

  const nonFakeSuspicion = inferNonFakeArtifactSuspicion(artifact, source);
  const showIntegrityPrecheckDisclaimer = shouldShowIntegrityPrecheckDisclaimer(source);
  const contentExpectationKey = artifactContentExpectationKey(
    artifact.artifactType,
    source,
    ctx.realPostgreSqlLogicalDumpConfigured,
  );

  return {
    artifactId: artifact.id,
    artifactClassLabelKey: artifactClassLabelKeyForType(artifact.artifactType, source, ctx.realPostgreSqlLogicalDumpConfigured),
    contentExpectationKey,
    sourceExecutionReality: source,
    recoverabilityUse,
    filePresence,
    download,
    nonFakeSuspicion,
    showIntegrityPrecheckDisclaimer,
    downloadProvenByApiOnly: true,
  };
}

/** Son çalıştırma başarısızken LKG indirmesi gösterilsin mi (recoverability run id ≠ latest id). */
/**
 * API’nin bildirdiği bayt boyutu — operatör “boş dosya” sanmasın diye tabloda gösterilir (Fake stub birkaç on bayt olabilir).
 */
export function formatArtifactByteSize(
  n: number | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  if (n == null || Number.isNaN(n)) return '—';
  if (n < 1024) return t('backupDr.latestRun.bytesB', { n: String(Math.round(n)) });
  const kb = n / 1024;
  if (kb < 1024) return t('backupDr.latestRun.bytesKb', { n: kb.toFixed(1) });
  const mb = kb / 1024;
  return t('backupDr.latestRun.bytesMb', { n: mb.toFixed(2) });
}

export function shouldOfferLastKnownGoodArtifactDownload(params: {
  latestRunId: string | undefined;
  latestStatus: number | undefined;
  lastSuccessfulBackupRunId: string | null | undefined;
}): boolean {
  const lkg = params.lastSuccessfulBackupRunId?.trim();
  if (!lkg) return false;
  const failed =
    params.latestStatus === 4 ||
    params.latestStatus === 5 ||
    params.latestStatus === 6;
  if (!failed) return false;
  if (params.latestRunId && lkg === params.latestRunId) return false;
  return true;
}
