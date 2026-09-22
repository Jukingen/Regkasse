import { redirect } from 'next/navigation';

import { BACKUP_AUDIT_PATH } from '@/shared/backupAreaRoutes';

/** Legacy alias — canonical route is `/backup/audit`. */
export default function BackupLogsRedirectPage() {
  redirect(BACKUP_AUDIT_PATH);
}
