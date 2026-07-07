/**
 * Backup & DR hub: canonical App Router paths for secondary nav and sidebar IA.
 *
 * Ownership:
 * - Sidebar IA: `SIDEBAR_NAV_ITEM_CATALOG` (`adminSidebarRegistry.ts`)
 * - Horizontal nav: `BackupSecondaryNav`
 * - Access: `ROUTE_PERMISSIONS` + virtual sidebar keys (`/backup/configuration/*`)
 * - Group open-state: `ADMIN_SIDEBAR_GROUP_ROUTES[grp-backup]`
 * - Legacy aliases: `/settings/backup-dr`, `/admin/backup` → redirect helpers in `backupLegacyRedirect.ts`
 */
import type { BackupManagementTabKey } from '@/features/backup-management/components/BackupManagementPanel';

export const BACKUP_DASHBOARD_PATH = '/backup/dashboard' as const;
export const BACKUP_RUNS_PATH = '/backup/runs' as const;
export const BACKUP_CONFIGURATION_PATH = '/backup/configuration' as const;
export const BACKUP_AUDIT_PATH = '/backup/audit' as const;

/** Sidebar section landing (dashboard). */
export const BACKUP_HUB_LANDING_PATH = BACKUP_DASHBOARD_PATH;

export const BACKUP_AREA_ROUTE_PATHS = [
    '/backup',
    BACKUP_DASHBOARD_PATH,
    BACKUP_RUNS_PATH,
    BACKUP_CONFIGURATION_PATH,
    BACKUP_AUDIT_PATH,
    /** Virtual sidebar keys — guarded via ROUTE_PERMISSIONS. */
    '/backup/configuration/schedule',
    '/backup/configuration/platform',
    /** Legacy redirects (still guarded). */
    '/settings/backup',
    '/settings/backup-dr',
    '/admin/backup',
] as const;

export type BackupAreaRoutePath = (typeof BACKUP_AREA_ROUTE_PATHS)[number];

/** Sidebar-only virtual keys (not separate App Router segments). */
export const BACKUP_SIDEBAR_VIRTUAL_KEYS = {
    schedule: '/backup/configuration/schedule',
    platform: '/backup/configuration/platform',
} as const;

export const BACKUP_SECONDARY_NAV_ITEMS = [
    {
        id: 'overview',
        menuKey: BACKUP_DASHBOARD_PATH,
        href: BACKUP_DASHBOARD_PATH,
        labelKey: 'nav.backupOverview',
    },
    {
        id: 'runs',
        menuKey: BACKUP_RUNS_PATH,
        href: BACKUP_RUNS_PATH,
        labelKey: 'nav.backupRuns',
    },
    {
        id: 'configuration',
        menuKey: BACKUP_CONFIGURATION_PATH,
        href: BACKUP_CONFIGURATION_PATH,
        labelKey: 'nav.backupConfiguration',
    },
    {
        id: 'auditLog',
        menuKey: BACKUP_AUDIT_PATH,
        href: BACKUP_AUDIT_PATH,
        labelKey: 'nav.backupAuditLog',
    },
] as const;

export function isBackupAreaPath(pathname: string | null | undefined): boolean {
    const p = (pathname ?? '').replace(/\/+$/, '') || '/';
    if (p === '/backup' || p.startsWith('/backup/')) return true;
    return (
        p === '/settings/backup-dr' ||
        p.startsWith('/settings/backup-dr/') ||
        p === '/settings/backup' ||
        p === '/admin/backup' ||
        p.startsWith('/admin/backup/')
    );
}

/** @deprecated Prefer route pathname; kept for legacy `?tab=` deep links during redirect. */
export function resolveBackupTabFromSearch(search: string | null | undefined): BackupManagementTabKey {
    const tab = new URLSearchParams(search ?? '').get('tab')?.trim().toLowerCase();
    if (tab === 'monitoring' || tab === 'configuration' || tab === 'log' || tab === 'operations') {
        return tab;
    }
    return 'operations';
}

export const BACKUP_SCHEDULE_SETTINGS_HREF =
    `${BACKUP_CONFIGURATION_PATH}#backup-dr-schedule-settings` as const;

export function backupPathFromPathname(pathname: string | null | undefined): string | null {
    const p = (pathname ?? '').replace(/\/+$/, '') || '/';
    if (p === BACKUP_DASHBOARD_PATH || p === '/backup') return BACKUP_DASHBOARD_PATH;
    if (p === BACKUP_RUNS_PATH) return BACKUP_RUNS_PATH;
    if (p === BACKUP_CONFIGURATION_PATH || p.startsWith(`${BACKUP_CONFIGURATION_PATH}/`)) {
        return BACKUP_CONFIGURATION_PATH;
    }
    if (p === BACKUP_AUDIT_PATH) return BACKUP_AUDIT_PATH;
    return null;
}
