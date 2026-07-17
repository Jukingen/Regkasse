/**
 * Backup feature public API — pages live under app routes; import hooks/components from here.
 */

export {
  backupQueryKeys,
  invalidateBackupQueries,
  useBackupConfigurationHealth,
  useBackupRun,
  useBackupRuns,
  useBackupSettings,
  useTriggerBackup,
  useUpdateBackupSettings,
  type BackupConfigurationHealthView,
  type BackupRunsParams,
  type BackupSettings as BackupSettingsDto,
  type TriggerBackupParams,
} from "@/features/backup/api/backupHooks";

export { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
export { useBackupList, type BackupListItemResponseDto } from "@/features/backup/hooks/useBackupList";
export { AdminBackupPage, AdminBackupPageHeaderActions } from "@/features/backup/pages/AdminBackupPage";
export { BackupRunsTable } from "@/features/backup/components/BackupRunsTable";
export { BackupList } from "@/features/backup/components/BackupList";
export { BackupProgress } from "@/features/backup/components/BackupProgress";
export { BackupDiff } from "@/features/backup/components/BackupDiff";
export { BackupDiffPanel } from "@/features/backup/components/BackupDiffPanel";
export { useBackupDiff } from "@/features/backup/hooks/useBackupDiff";
export { TenantBackupView } from "@/features/backup/components/TenantBackupView";
export { SystemBackupView } from "@/features/backup/components/SystemBackupView";
export { useBackupProgress } from "@/features/backup/hooks/useBackupProgress";
export { useBackupPerformance } from "@/features/backup/hooks/useBackupPerformance";
export { BackupPerformanceDashboard } from "@/features/backup/components/BackupPerformanceDashboard";
export { BackupDetailModal } from "@/features/backup/components/BackupDetailModal";
export { BackupConfigurationForm } from "@/features/backup/components/BackupConfigurationForm";
export { BackupSettings } from "@/features/backup/components/BackupSettings";
export { BackupSchedulePlanner } from "@/features/backup/components/BackupSchedulePlanner";
export { TriggerBackupButton } from "@/features/backup/components/TriggerBackupButton";
export { BackupVerificationReport } from "@/features/backup/components/BackupVerificationReport";
export { useBackupVerificationReport } from "@/features/backup/hooks/useBackupVerificationReport";
export { RestoreModal, type RestoreModalBackup, type RestoreModalProps } from "@/features/backup/components/RestoreModal";
export { RestorePreview } from "@/features/backup/components/RestorePreview";
export { RestoreHistoryView } from "@/features/backup/components/RestoreHistoryView";
export { BackupComplianceDashboard } from "@/features/backup/components/BackupComplianceDashboard";
export { useRestorePreview } from "@/features/backup/hooks/useRestorePreview";
export { useRestoreComplianceCheck } from "@/features/backup/hooks/useRestoreComplianceCheck";
export { useRestoreHistory } from "@/features/backup/hooks/useRestoreHistory";
export { useComplianceStatus } from "@/features/backup/hooks/useComplianceStatus";
export { PitrRestoreModal, type PitrRestorePayload } from "@/features/backup/components/PitrRestoreModal";
export { PitrRestoreWorkflow } from "@/features/backup/components/PitrRestoreWorkflow";
export { triggerPitrRestoreWithApproval } from "@/features/backup/logic/pitrRestoreApproval";
export {
  getPitrAvailability,
  validatePitrRestorePoint,
  type PitrAvailabilityResponse,
  type RestorePointValidationResult,
} from "@/features/backup/logic/backupPitrApi";
export { ConfigurationHealthCard } from "@/features/backup/components/ConfigurationHealthCard";
export { BackupHistoryChart } from "@/features/backup/components/BackupHistoryChart";
