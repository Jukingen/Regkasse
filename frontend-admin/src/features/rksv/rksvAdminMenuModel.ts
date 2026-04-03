/**
 * RKSV admin menu groups (sidebar + hub). Route paths and menu keys stay stable for
 * selectedKeys and MENU_PERMISSION. Labels use i18n except verifications (operator truth).
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
    let p = (pathname ?? '').replace(/\/+$/, '');
    if (p === '') p = '/';
    if (!p.startsWith('/rksv')) return [];
    if (p === '/rksv') return [rksvGroupSubMenuKey('daily')];
    for (const g of groups) {
        for (const item of g.items) {
            /** Hub href `/rksv` must not prefix-match deeper routes like `/rksv/incident`. */
            const prefixOk = item.href !== '/rksv' && p.startsWith(`${item.href}/`);
            if (p === item.href || prefixOk) {
                return [rksvGroupSubMenuKey(g.id)];
            }
        }
    }
    return [rksvGroupSubMenuKey('daily')];
}

export function collectRksvMenuLeafKeys(groups: RksvMenuGroup[]): string[] {
    return groups.flatMap((g) => g.items.map((i) => i.key));
}

/**
 * @param t — i18n translate for `nav.*` keys
 * @param verificationNavLabel — from OPERATOR_VERIFICATIONS_COPY.navMenuLabel
 */
export function buildRksvMenuGroups(t: (key: string) => string, verificationNavLabel: string): RksvMenuGroup[] {
    return [
        {
            id: 'daily',
            groupLabel: t('nav.rksvGroupOperativ'),
            hubTaskLine:
                'Tagesgeschäft: Kassenübersicht, Outbox-/SOAP-Pipeline (primär) und optional Zahlungszeilen-Legacy.',
            items: [
                { key: '/rksv/operations', href: '/rksv', label: t('nav.rksvLeafOverview') },
                {
                    key: '/rksv/finanz-online-outbox',
                    href: '/rksv/finanz-online-outbox',
                    label: t('nav.finanzOnlineOutbox'),
                },
                {
                    key: '/rksv/finanz-online-queue',
                    href: '/rksv/finanz-online-queue',
                    label: t('nav.rksvLeafLegacyQueue'),
                },
            ],
        },
        {
            id: 'investigation',
            groupLabel: t('nav.rksvGroupVorfaelle'),
            hubTaskLine:
                'Störungen eingrenzen: Incident, Replay, Hash-Konflikte und Audit-Spur (Signatur/Offline).',
            items: [
                { key: '/rksv/incident', href: '/rksv/incident', label: t('nav.rksvLeafIncident') },
                { key: '/rksv/replay-batch', href: '/rksv/replay-batch', label: t('nav.rksvLeafReplayBatch') },
                {
                    key: '/rksv/payload-hash-conflicts',
                    href: '/rksv/payload-hash-conflicts',
                    label: t('nav.rksvLeafHashConflicts'),
                },
                { key: '/rksv/verifications', href: '/rksv/verifications', label: verificationNavLabel },
            ],
        },
        {
            id: 'diagnostics',
            groupLabel: t('nav.rksvGroupDiagnose'),
            hubTaskLine: 'Support und Integrität: Export-Risiko, DB-weite Checks und Offline-Abdeckung prüfen.',
            items: [
                {
                    key: '/rksv/fiscal-export-diagnostics',
                    href: '/rksv/fiscal-export-diagnostics',
                    label: t('nav.rksvLeafFiscalExport'),
                },
                { key: '/rksv/integrity', href: '/rksv/integrity', label: t('nav.rksvLeafDataIntegrity') },
                {
                    key: '/rksv/offline-intent-coverage',
                    href: '/rksv/offline-intent-coverage',
                    label: t('nav.rksvLeafOfflineCoverage'),
                },
            ],
        },
        {
            id: 'config',
            groupLabel: t('nav.rksvGroupAnbindung'),
            hubTaskLine:
                'Schnittstellen und Konfiguration: Status, Zertifikat und Integrations-/Diagnose-Oberfläche (nicht die Abgleichsliste).',
            items: [
                { key: '/rksv/status', href: '/rksv/status', label: t('nav.rksvLeafSystemStatus') },
                { key: '/rksv/cmc-certificate', href: '/rksv/cmc-certificate', label: t('nav.rksvLeafCmcCertificate') },
                {
                    key: '/rksv/finanz-online-operations',
                    href: '/rksv/finanz-online-operations',
                    label: t('nav.rksvLeafFinanzOnlineIntegration'),
                },
            ],
        },
    ];
}
