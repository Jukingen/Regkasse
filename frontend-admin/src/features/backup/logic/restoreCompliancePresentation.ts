/**
 * Maps compliance-check API names to i18n keys / alert presentation.
 */
import type { RestoreComplianceCheckItemDto } from '@/features/backup-dr/logic/manualRestoreApi';

export type ComplianceAlertTone = 'success' | 'warning' | 'error' | 'info';

export function complianceCheckLabelKey(checkName: string): string {
  switch (checkName) {
    case 'SameTenant':
      return 'backupDr.manualRestore.restorePreview.compliance.checks.sameTenant';
    case 'BackupIntegrity':
      return 'backupDr.manualRestore.restorePreview.compliance.checks.backupIntegrity';
    case 'RksvValidationGate':
      return 'backupDr.manualRestore.restorePreview.compliance.checks.rksvGate';
    case 'ArtifactStrategy':
      return 'backupDr.manualRestore.restorePreview.compliance.checks.artifactStrategy';
    default:
      return 'backupDr.manualRestore.restorePreview.compliance.checks.unknown';
  }
}

export function complianceAlertTone(args: {
  isLoading: boolean;
  isError: boolean;
  succeeded: boolean | undefined;
}): ComplianceAlertTone {
  if (args.isLoading) return 'info';
  if (args.isError) return 'error';
  if (args.succeeded === true) return 'success';
  if (args.succeeded === false) return 'error';
  return 'warning';
}

export function sortComplianceChecks(
  checks: RestoreComplianceCheckItemDto[] | undefined
): RestoreComplianceCheckItemDto[] {
  if (!checks?.length) return [];
  const order = ['SameTenant', 'BackupIntegrity', 'ArtifactStrategy', 'RksvValidationGate'];
  return [...checks].sort((a, b) => order.indexOf(a.name) - order.indexOf(b.name));
}
