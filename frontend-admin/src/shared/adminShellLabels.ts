/** Stable href for the admin overview (dashboard) breadcrumb. */
export const ADMIN_OVERVIEW_HREF = '/dashboard' as const;

/** i18n key for overview breadcrumb title (`common.breadcrumb.overview`). */
export const ADMIN_OVERVIEW_BREADCRUMB_KEY = 'common.breadcrumb.overview' as const;

/** Build overview breadcrumb with active locale (preferred over hardcoded German). */
export function adminOverviewCrumb(t: (key: string) => string) {
    return { title: t(ADMIN_OVERVIEW_BREADCRUMB_KEY), href: ADMIN_OVERVIEW_HREF } as const;
}

/** Backward-compatible default (de) overview breadcrumb for pages not yet wired to `t`. */
export const ADMIN_OVERVIEW_CRUMB = { title: 'Übersicht', href: ADMIN_OVERVIEW_HREF } as const;

/** Sidebar section key map (non-route keys); keep in sync with grouped layout menu. */
export const ADMIN_NAV_GROUP_LABEL_KEYS = {
    kasseBelege: 'adminShell.group.kasseBelege',
    sortiment: 'adminShell.group.sortiment',
    kundenVorteile: 'adminShell.group.kundenVorteile',
    verwaltung: 'adminShell.group.verwaltung',
    rksv: 'adminShell.group.rksv',
} as const;

/** Backward-compatible default (de) sidebar section labels. */
export const ADMIN_NAV_GROUP_LABELS = {
    kasseBelege: 'Kasse & Belege',
    sortiment: 'Sortiment',
    kundenVorteile: 'Kunden & Vorteile',
    verwaltung: 'Verwaltung',
    rksv: 'RKSV',
} as const;

/** Main sidebar link translation key map. */
export const ADMIN_NAV_LABEL_KEYS = {
    overview: 'nav.overview',
    invoices: 'nav.invoices',
    products: 'nav.products',
    modifierGroups: 'nav.modifierGroups',
    categories: 'nav.categories',
    customers: 'nav.customers',
    benefitDefinitions: 'nav.benefitDefinitions',
    benefitAssignments: 'nav.benefitAssignments',
    receipts: 'nav.receipts',
    receiptTemplates: 'nav.receiptTemplates',
    receiptGenerate: 'nav.receiptGenerate',
    auditLogs: 'nav.auditLogs',
    payments: 'nav.payments',
    tagesabschluss: 'nav.tagesabschluss',
    users: 'nav.users',
    settings: 'nav.settings',
    myProfile: 'nav.myProfile',
    logout: 'nav.logout',
    rksvOperationsOverview: 'nav.rksvOperationsOverview',
    finanzOnlineAbgleich: 'nav.finanzOnlineAbgleich',
} as const;

/** Backward-compatible default (de) navigation labels. */
export const ADMIN_NAV_LABELS = {
    overview: 'Übersicht',
    invoices: 'Rechnungen',
    products: 'Produkte',
    modifierGroups: 'Add-on-Gruppen',
    categories: 'Kategorien',
    customers: 'Kunden',
    benefitDefinitions: 'Vorteile (Definitionen)',
    benefitAssignments: 'Vorteile (Zuweisungen)',
    receipts: 'Belege',
    receiptTemplates: 'Belegvorlagen',
    receiptGenerate: 'Belegvorschau',
    auditLogs: 'Audit-Protokoll',
    payments: 'Zahlungen',
    tagesabschluss: 'Tagesabschluss',
    users: 'Benutzer',
    settings: 'Einstellungen',
    myProfile: 'Mein Profil',
    logout: 'Abmelden',
    rksvOperationsOverview: 'RKSV Übersicht',
    finanzOnlineAbgleich: 'FinanzOnline-Abgleich',
} as const;
