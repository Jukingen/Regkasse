import { redirect } from 'next/navigation';
import { BACKUP_CONFIGURATION_PATH } from '@/shared/backupAreaRoutes';

/** Legacy alias — canonical route is `/backup/configuration`. */
export default function BackupConfigRedirectPage() {
  redirect(BACKUP_CONFIGURATION_PATH);
}
