import { redirect } from 'next/navigation';

import { BACKUP_RUNS_PATH } from '@/shared/backupAreaRoutes';

/** Legacy `/admin/backup` → canonical backup runs list. */
export default function LegacyAdminBackupRedirect() {
  redirect(BACKUP_RUNS_PATH);
}
