/**
 * Single source for RKSV sidebar groups and hub links. Route paths and menu keys stay stable for
 * selectedKeys and MENU_PERMISSION lookups.
 */

export type RksvMenuLeaf = {
    /** Ant Design Menu item key — must match pathname or legacy mapping (e.g. /rksv → /rksv/operations). */
    key: string;
    href: string;
    label: string;
};

export type RksvMenuGroup = {
    id: string;
    groupLabel: string;
    /** One-line task framing for the RKSV landing hub (German, de-DE); sidebar ignores this field. */
    hubTaskLine: string;
    items: RksvMenuLeaf[];
};

/** Ant Design SubMenu key for an RKSV IA group (stable for openKeys). */
export function rksvGroupSubMenuKey(groupId: string): string {
    return `rksv-grp-${groupId}`;
}

/**
 * Subgroup keys to open for a pathname so the active leaf stays under a visible SubMenu.
 */
export function getRksvOpenSubgroupKeys(pathname: string | null | undefined, groups: RksvMenuGroup[]): string[] {
    const p = pathname ?? '';
    if (!p.startsWith('/rksv')) return [];
    if (p === '/rksv') return [rksvGroupSubMenuKey('daily')];
    for (const g of groups) {
        for (const item of g.items) {
            if (p === item.href || p.startsWith(`${item.href}/`)) {
                return [rksvGroupSubMenuKey(g.id)];
            }
        }
    }
    return [rksvGroupSubMenuKey('daily')];
}

/**
 * @param verificationNavLabel — from OPERATOR_VERIFICATIONS_COPY.navMenuLabel (keeps menu + hub in sync).
 */
export function buildRksvMenuGroups(verificationNavLabel: string): RksvMenuGroup[] {
    return [
        {
            id: 'daily',
            /** Daily operations: hub + payment-level FO reconciliation */
            groupLabel: 'Operativ',
            hubTaskLine:
                'Tagesgeschäft: Kassenübersicht, Outbox-/SOAP-Pipeline (primär) und optional Zahlungszeilen-Legacy.',
            items: [
                { key: '/rksv/operations', href: '/rksv', label: 'Übersicht' },
                {
                    key: '/rksv/finanz-online-outbox',
                    href: '/rksv/finanz-online-outbox',
                    label: 'FinanzOnline · Outbox',
                },
                {
                    key: '/rksv/finanz-online-queue',
                    href: '/rksv/finanz-online-queue',
                    label: 'Zahlungs-Abgleich (Legacy)',
                },
            ],
        },
        {
            id: 'investigation',
            /** Correlation, replay, conflicts, audit trail */
            groupLabel: 'Vorfälle',
            hubTaskLine:
                'Störungen eingrenzen: Incident, Replay, Hash-Konflikte und Audit-Spur (Signatur/Offline).',
            items: [
                { key: '/rksv/incident', href: '/rksv/incident', label: 'Incident' },
                { key: '/rksv/replay-batch', href: '/rksv/replay-batch', label: 'Replay-Batch' },
                { key: '/rksv/payload-hash-conflicts', href: '/rksv/payload-hash-conflicts', label: 'Payload-Hash' },
                { key: '/rksv/verifications', href: '/rksv/verifications', label: verificationNavLabel },
            ],
        },
        {
            id: 'diagnostics',
            /** Support diagnostics and integrity tooling */
            groupLabel: 'Diagnose',
            hubTaskLine: 'Support und Integrität: Export-Risiko, DB-weite Checks und Offline-Abdeckung prüfen.',
            items: [
                {
                    key: '/rksv/fiscal-export-diagnostics',
                    href: '/rksv/fiscal-export-diagnostics',
                    label: 'Fiscal-Export',
                },
                { key: '/rksv/integrity', href: '/rksv/integrity', label: 'Datenintegrität' },
                { key: '/rksv/offline-intent-coverage', href: '/rksv/offline-intent-coverage', label: 'Offline-Intent' },
            ],
        },
        {
            id: 'config',
            /** Connection, certificates, integration surface (not row-level FO list) */
            groupLabel: 'Anbindung',
            hubTaskLine:
                'Schnittstellen und Konfiguration: Status, Zertifikat und Integrations-/Diagnose-Oberfläche (nicht die Abgleichsliste).',
            items: [
                { key: '/rksv/status', href: '/rksv/status', label: 'Systemstatus' },
                { key: '/rksv/cmc-certificate', href: '/rksv/cmc-certificate', label: 'CMC / Zertifikat' },
                {
                    key: '/rksv/finanz-online-operations',
                    href: '/rksv/finanz-online-operations',
                    label: 'FinanzOnline · Integration',
                },
            ],
        },
    ];
}
