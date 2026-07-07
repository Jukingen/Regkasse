import { redirect } from 'next/navigation';
import { backupRedirectFromLegacySearch } from '@/shared/backupLegacyRedirect';

type PageProps = {
    searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/** Legacy `/settings/backup-dr` → canonical `/backup/*` (preserves `runId`, maps `tab`). */
export default async function LegacyBackupDrRedirect({ searchParams }: PageProps) {
    const sp = await searchParams;
    const qs = new URLSearchParams();
    for (const [key, value] of Object.entries(sp)) {
        if (typeof value === 'string') qs.set(key, value);
        else if (Array.isArray(value) && value[0]) qs.set(key, value[0]);
    }
    redirect(backupRedirectFromLegacySearch(qs.toString()));
}
