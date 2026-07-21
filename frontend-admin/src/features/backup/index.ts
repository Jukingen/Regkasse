/**
 * Backup feature public API — pages live under app routes; import hooks/components from here.
 */

export {
  type BackupConfigurationHealthView,
  backupQueryKeys,
  type BackupRunsParams,
  type BackupSettings as BackupSettingsDto,
  invalidateBackupQueries,
  type TriggerBackupParams,
  useBackupConfigurationHealth,
  useBackupRun,
  useBackupRuns,
  useBackupSettings,
  useTriggerBackup,
  useUpdateBackupSettings,
} from '@/features/backup/api/backupHooks';
export { BackupComplianceDashboard } from '@/features/backup/components/BackupComplianceDashboard';
export { BackupConfigurationForm } from '@/features/backup/components/BackupConfigurationForm';
export { BackupDetailModal } from '@/features/backup/components/BackupDetailModal';
export { BackupDiff } from '@/features/backup/components/BackupDiff';
export { BackupDiffPanel } from '@/features/backup/components/BackupDiffPanel';
export { BackupHistoryChart } from '@/features/backup/components/BackupHistoryChart';
export { BackupList } from '@/features/backup/components/BackupList';
export { BackupPerformanceDashboard } from '@/features/backup/components/BackupPerformanceDashboard';
export { BackupProgress } from '@/features/backup/components/BackupProgress';
export { BackupRunsTable } from '@/features/backup/components/BackupRunsTable';
export { BackupSchedulePlanner } from '@/features/backup/components/BackupSchedulePlanner';
export { BackupSettings } from '@/features/backup/components/BackupSettings';
export { BackupVerificationReport } from '@/features/backup/components/BackupVerificationReport';
export { ConfigurationHealthCard } from '@/features/backup/components/ConfigurationHealthCard';
export {
  PitrRestoreModal,
  type PitrRestorePayload,
} from '@/features/backup/components/PitrRestoreModal';
export { PitrRestoreWorkflow } from '@/features/backup/components/PitrRestoreWorkflow';
export { RestoreHistoryView } from '@/features/backup/components/RestoreHistoryView';
export {
  RestoreModal,
  type RestoreModalBackup,
  type RestoreModalProps,
} from '@/features/backup/components/RestoreModal';
export { RestorePreview } from '@/features/backup/components/RestorePreview';
export { SystemBackupView } from '@/features/backup/components/SystemBackupView';
export { TenantBackupView } from '@/features/backup/components/TenantBackupView';
export { TriggerBackupButton } from '@/features/backup/components/TriggerBackupButton';
export { useBackupDiff } from '@/features/backup/hooks/useBackupDiff';
export {
  type BackupListItemResponseDto,
  useBackupList,
} from '@/features/backup/hooks/useBackupList';
export { useBackupPerformance } from '@/features/backup/hooks/useBackupPerformance';
export { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
export { useBackupProgress } from '@/features/backup/hooks/useBackupProgress';
export { useBackupVerificationReport } from '@/features/backup/hooks/useBackupVerificationReport';
export { useComplianceStatus } from '@/features/backup/hooks/useComplianceStatus';
export { useRestoreComplianceCheck } from '@/features/backup/hooks/useRestoreComplianceCheck';
export { useRestoreHistory } from '@/features/backup/hooks/useRestoreHistory';
export { useRestorePreview } from '@/features/backup/hooks/useRestorePreview';
export {
  getPitrAvailability,
  type PitrAvailabilityResponse,
  type RestorePointValidationResult,
  validatePitrRestorePoint,
} from '@/features/backup/logic/backupPitrApi';
export { triggerPitrRestoreWithApproval } from '@/features/backup/logic/pitrRestoreApproval';
export {
  AdminBackupPage,
  AdminBackupPageHeaderActions,
} from '@/features/backup/pages/AdminBackupPage';
