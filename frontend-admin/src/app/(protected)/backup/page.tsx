import { redirect } from 'next/navigation';
import { BACKUP_DASHBOARD_PATH } from '@/shared/backupAreaRoutes';

export default function BackupRootPage() {
    redirect(BACKUP_DASHBOARD_PATH);
}
