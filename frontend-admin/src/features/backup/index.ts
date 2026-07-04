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
  type BackupSettings,
  type TriggerBackupParams,
} from "@/features/backup/api/backupHooks";

export { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
export { useBackupList, type BackupListItemResponseDto } from "@/features/backup/hooks/useBackupList";
export { AdminBackupPage, AdminBackupPageHeaderActions } from "@/features/backup/pages/AdminBackupPage";
export { BackupRunsTable } from "@/features/backup/components/BackupRunsTable";
export { BackupList } from "@/features/backup/components/BackupList";
export { BackupDetailModal } from "@/features/backup/components/BackupDetailModal";
export { BackupConfigurationForm } from "@/features/backup/components/BackupConfigurationForm";
export { BackupSettings } from "@/features/backup/components/BackupSettings";
export { BackupSchedulePlanner } from "@/features/backup/components/BackupSchedulePlanner";
export { TriggerBackupButton } from "@/features/backup/components/TriggerBackupButton";
export { BackupVerificationReport } from "@/features/backup/components/BackupVerificationReport";
export { useBackupVerificationReport } from "@/features/backup/hooks/useBackupVerificationReport";
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
