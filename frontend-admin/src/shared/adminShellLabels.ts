/**
 * Canonical de-DE labels for admin shell (sidebar, breadcrumbs).
 * Routes stay as in the app router; operator-visible wording is German.
 */
export const ADMIN_OVERVIEW_CRUMB = { title: 'Übersicht', href: '/dashboard' } as const;

/** Sidebar section titles (non-route keys); keep in sync with grouped layout menu. */
export const ADMIN_NAV_GROUP_LABELS = {
    kasseBelege: 'Kasse & Belege',
    sortiment: 'Sortiment',
    kundenVorteile: 'Kunden & Vorteile',
    verwaltung: 'Verwaltung',
    rksv: 'RKSV',
} as const;

/** Main sidebar link labels (must match breadcrumb first segment for Übersicht). */
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
    /** Breadcrumb + page titles: RKSV landing (`/rksv`); align with sidebar RKSV hub. */
    rksvOperationsOverview: 'RKSV Übersicht',
    /** Sidebar + breadcrumbs: FinanzOnline reconciliation list (`/rksv/finanz-online-queue`). */
    finanzOnlineAbgleich: 'FinanzOnline-Abgleich',
} as const;
