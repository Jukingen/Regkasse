import type { BackupManagementTabKey } from '@/features/backup-management/components/BackupManagementPanel';
import {
    BACKUP_AUDIT_PATH,
    BACKUP_CONFIGURATION_PATH,
    BACKUP_DASHBOARD_PATH,
    BACKUP_RUNS_PATH,
} from '@/shared/backupAreaRoutes';

/** Maps legacy `?tab=` values to canonical `/backup/*` routes. */
export function backupCanonicalPathFromLegacyTab(tab: string | undefined): string {
    const normalized = tab?.trim().toLowerCase();
    switch (normalized as BackupManagementTabKey | undefined) {
        case 'configuration':
            return BACKUP_CONFIGURATION_PATH;
        case 'log':
            return BACKUP_AUDIT_PATH;
        case 'monitoring':
            return BACKUP_RUNS_PATH;
        case 'operations':
        default:
            return BACKUP_DASHBOARD_PATH;
    }
}

/** Builds redirect target from legacy `/settings/backup-dr` query string. */
export function backupRedirectFromLegacySearch(search: string): string {
    const sp = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
    const tab = sp.get('tab') ?? undefined;
    const runId = sp.get('runId')?.trim();
    sp.delete('tab');

    const base = backupCanonicalPathFromLegacyTab(tab);
    const out = new URLSearchParams();
    if (runId) {
        out.set('runId', runId);
    }
    for (const [key, value] of sp.entries()) {
        if (key !== 'runId') out.set(key, value);
    }
    const qs = out.toString();
    return qs ? `${base}?${qs}` : base;
}
