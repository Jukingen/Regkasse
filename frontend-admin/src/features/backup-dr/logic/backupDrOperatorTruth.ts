/**
 * Operatör-doğruluk: harici özet ve zaman damgası yardımcıları.
 * Ana görünüm modeli: `backupDrOperatorTruthModel.ts` (`buildBackupOperatorTruthModel`).
 */
import {
  type ExternalCopyVariant,
  externalCopyVariantToI18nKey,
  mapArtifactsToExternalCopyVariant,
} from '@/features/backup-dr/logic/backupDrMappers';
import type { OperatorTruthTranslate } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';

export type { OperatorTruthTranslate } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
export {
  automatedRestoreCapabilityFromStatus,
  type AutomatedRestoreCapabilityModel,
  type BackupOperatorTruthModel,
  buildBackupOperatorTruthModel,
  buildOperatorTruthBanner,
  type BuildOperatorTruthBannerParams,
  hasRecoverabilityProofGaps,
  type OperatorTruthBannerModel,
} from '@/features/backup-dr/logic/backupDrOperatorTruthModel';

export interface ExternalCopyOperatorSummary {
  variant: ExternalCopyVariant;
  /** i18n anahtarı — metin açıkça “kanıt değil, lifecycle meta” der. */
  textKey: string;
}

export function summarizeExternalCopyForOperator(
  artifacts: Parameters<typeof mapArtifactsToExternalCopyVariant>[0]
): ExternalCopyOperatorSummary {
  const variant = mapArtifactsToExternalCopyVariant(artifacts);
  const textKey = externalCopyVariantToI18nKey(variant);
  return { variant, textKey };
}

/** Dashboard istatistik satırı: harici özet metni. */
export function externalCopySummaryText(
  artifacts: Parameters<typeof mapArtifactsToExternalCopyVariant>[0],
  t: OperatorTruthTranslate
): { variant: ExternalCopyVariant; text: string } {
  const { variant, textKey } = summarizeExternalCopyForOperator(artifacts);
  return { variant, text: t(textKey) };
}

/**
 * Özet kartında null alanlar için “kanıt yok” — yeşil varsayım yok.
 */
export function formatRecoverabilityTimestampOrProofGap(
  iso: string | null | undefined,
  formatDt: (iso: string | undefined | null, locale: string) => string,
  formatLocale: string,
  t: OperatorTruthTranslate
): string {
  if (!iso) return t('backupDr.operatorTruth.noTimestampFromApi');
  return formatDt(iso, formatLocale);
}
