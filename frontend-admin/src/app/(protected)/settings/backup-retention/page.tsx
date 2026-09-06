import { redirect } from 'next/navigation';

import { BACKUP_RETENTION_PATH } from '@/shared/backupAreaRoutes';

/** Canonical retention settings live in the backup hub. */
export default function SettingsBackupRetentionPage() {
  redirect(BACKUP_RETENTION_PATH);
}
