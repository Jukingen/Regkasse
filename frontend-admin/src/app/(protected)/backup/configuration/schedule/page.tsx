import { redirect } from 'next/navigation';

import { BACKUP_CONFIGURATION_PATH } from '@/shared/backupAreaRoutes';

/** Legacy virtual sidebar key → canonical configuration route (schedule anchor). */
export default function BackupConfigurationScheduleRedirect() {
  redirect(`${BACKUP_CONFIGURATION_PATH}#backup-dr-schedule-settings`);
}
