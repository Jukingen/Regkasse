import { redirect } from 'next/navigation';
import { BACKUP_CONFIGURATION_PATH } from '@/shared/backupAreaRoutes';

/** Legacy virtual sidebar key → platform settings section on configuration page. */
export default function BackupConfigurationPlatformRedirect() {
    redirect(BACKUP_CONFIGURATION_PATH);
}
