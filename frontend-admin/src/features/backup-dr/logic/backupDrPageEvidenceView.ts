/**
 * Backup & DR sayfası: kanıt yüzeyi için birleşik görünüm girişi — anlam kaynakları
 * `buildDrProofPresentationModel` + `deriveBackupEvidenceLadder` üzerinden kalır; yeni iş kuralı eklemez.
 */
import type { BackupEvidenceLadderModel } from '@/features/backup-dr/logic/backupDrEvidenceLadder';
import type { DrProofPresentationModel } from '@/features/backup-dr/logic/drProofLevelPresentation';

/** Tek kartta L0–L6 + operasyonel kanıt merdivenini sunmak için props çantası. */
export interface BackupDrUnifiedEvidenceModels {
  drProof: DrProofPresentationModel;
  evidenceLadder: BackupEvidenceLadderModel;
}
