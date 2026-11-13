/**
 * Bakış seviyesi (glance) sunumu: yeşil Ant Design `Alert` / üst şerit, operatörde “iş bitti / tam güven”
 * yanlış okumasına yol açmasın — anlamsal ton `backupDrOperatorTruthModel` ve kanıt merdiveninde kalır,
 * yalnızca görsel öncelik nötrleştirilir.
 */
import type { EvidenceHeadlineTone } from '@/features/backup-dr/logic/backupDrEvidenceLadder';
import type { ExternalCopyVariant } from '@/features/backup-dr/logic/backupDrMappers';

/** Üst operatör doğruluk şeridi: “güçlü sinyaller” bile yeşil tamamlanma gibi görünmesin. */
export function mapOperatorValidityStripToAlertType(
  severity: 'warning' | 'info' | 'success'
): 'warning' | 'info' {
  if (severity === 'warning') return 'warning';
  if (severity === 'success') return 'info';
  return 'info';
}

/** Kanıt merdiveni özet satırı: `strongWithinApi` metin olarak kalır; çerçeve yeşil üretim başarısı gibi okunmasın. */
export function mapEvidenceHeadlineToneToAlertType(tone: EvidenceHeadlineTone): 'warning' | 'info' {
  if (tone === 'warning') return 'warning';
  if (tone === 'success') return 'info';
  return 'info';
}

/** Gerçek pg_dump yolu aktif bilgisi — önkoşul / teknik gerçek; kurtarma garantisi değil (yeşil “başarı” verme). */
export const REAL_DUMP_PATH_BANNER_ALERT_TYPE: 'info' = 'info';

/**
 * Harici arşiv kartı: `externalLifecycleOk` yalnızca metadata / yaşam döngüsü — “tamamlandı” gibi okunmasın;
 * başarısız veya karışık ile aynı uyarı tonu (içerik metni zaten sınırlayıcı).
 */
export function mapExternalCopyVariantToAlertType(
  variant: ExternalCopyVariant
): 'warning' | 'info' {
  if (variant === 'failed' || variant === 'mixed' || variant === 'externalLifecycleOk')
    return 'warning';
  return 'info';
}
