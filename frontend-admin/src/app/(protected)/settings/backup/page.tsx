import { redirect } from 'next/navigation';

import { BACKUP_DASHBOARD_PATH } from '@/shared/backupAreaRoutes';

/** Legacy `/settings/backup` → canonical backup dashboard. */
export default function SettingsBackupPage() {
  redirect(BACKUP_DASHBOARD_PATH);
}
