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
export { AdminBackupPage, AdminBackupPageHeaderActions } from "@/features/backup/pages/AdminBackupPage";
export { BackupRunsTable } from "@/features/backup/components/BackupRunsTable";
export { BackupDetailModal } from "@/features/backup/components/BackupDetailModal";
export { BackupConfigurationForm } from "@/features/backup/components/BackupConfigurationForm";
export { TriggerBackupButton } from "@/features/backup/components/TriggerBackupButton";
export { ConfigurationHealthCard } from "@/features/backup/components/ConfigurationHealthCard";
export { BackupHistoryChart } from "@/features/backup/components/BackupHistoryChart";
