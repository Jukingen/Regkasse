import { redirect } from 'next/navigation';

/** Canonical backup settings UI lives at `/settings/backup-dr`. */
export default function SettingsBackupPage() {
    redirect('/settings/backup-dr');
}
