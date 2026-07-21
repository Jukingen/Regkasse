import type { BackupExecutionModeRadioValue } from '@/features/backup-dr/logic/backupDrExecutionModePresentation';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

export function initialExecutionModeSelection(
  d: BackupExecutionModeResponseDto
): BackupExecutionModeRadioValue {
  const u = (d.requestedUserFacingMode ?? '').trim();
  if (u === 'Fake') return 'SimulatedFake';
  if (u === 'RealPgDump') return 'PostgreSqlPgDump';
  if (u === 'UseConfigurationDefault') return 'InheritFromConfiguration';
  const x = (d.storedMode ?? '').trim();
  if (x === 'SimulatedFake' || x === 'PostgreSqlPgDump' || x === 'InheritFromConfiguration') {
    return x;
  }
  return 'InheritFromConfiguration';
}

export function toPutExecutionModeString(mode: BackupExecutionModeRadioValue): string {
  switch (mode) {
    case 'InheritFromConfiguration':
      return 'UseConfigurationDefault';
    case 'SimulatedFake':
      return 'Fake';
    case 'PostgreSqlPgDump':
      return 'RealPgDump';
    default:
      return 'UseConfigurationDefault';
  }
}

export function executionModeSelectLabel(
  mode: BackupExecutionModeRadioValue,
  t: (key: string) => string
): string {
  switch (mode) {
    case 'SimulatedFake':
      return t('backupDr.configForm.executionModeOptions.fake');
    case 'PostgreSqlPgDump':
      return t('backupDr.configForm.executionModeOptions.pgDump');
    default:
      return t('backupDr.configForm.executionModeOptions.inherit');
  }
}
