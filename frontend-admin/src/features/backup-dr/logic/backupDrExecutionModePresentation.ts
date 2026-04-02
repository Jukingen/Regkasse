/**
 * Yedek çalıştırma modu API yanıtı → operatör için net senaryo metinleri ve seçilebilirlik yardımcıları.
 */

import type { BackupExecutionModeResponseDto, BackupExecutionSelectableModeDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

export { isRealRequestedNonRunnableState } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';

/** Radio değeri → API kullanıcı modu (PUT). */
export type BackupExecutionModeRadioValue =
  | 'InheritFromConfiguration'
  | 'SimulatedFake'
  | 'PostgreSqlPgDump';

export function findSelectableRow(
  modes: BackupExecutionSelectableModeDto[] | undefined,
  userFacing: string,
): BackupExecutionSelectableModeDto | undefined {
  return modes?.find((m) => m.userFacingMode === userFacing);
}

/**
 * Fake’e geçişte güçlü uyarı: API, üretim benzeri ortamda seçilebilir Fake için blockReason ile açıklama verir (onay kutusu gerekir).
 */
export function fakeSwitchNeedsStrongWarning(
  selected: BackupExecutionModeRadioValue,
  fakeRow: BackupExecutionSelectableModeDto | undefined,
): boolean {
  if (selected !== 'SimulatedFake' || !fakeRow?.selectable) return false;
  return Boolean((fakeRow.blockReason ?? '').trim());
}
