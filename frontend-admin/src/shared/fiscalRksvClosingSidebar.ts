/**
 * Sidebar metadata for “Fiskal & RKSV” closing / formal reports grouping (German labels via i18n).
 * Tooltip copy is intentionally Turkish per product guidance for this menu.
 */

export type FiscalRksvClosingBadgeKind = 'daily' | 'monthly' | 'yearly' | 'one-time';

/** Virtual menu keys: must match ROUTE_PERMISSIONS + permission filtering; href may differ (query focus). */
export const FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS = [
    '/rksv/sb/startbeleg',
    '/rksv/sb/monatsbeleg',
    '/rksv/sb/jahresbeleg',
    '/rksv/sb/nullbeleg',
    '/rksv/sb/schlussbeleg',
    '/rksv/sb/test-helper',
] as const;

export type FiscalRksvClosingSidebarLeaf = {
    menuKey: string;
    href: string;
    labelKey: string;
    emoji: string;
    accentColor: string;
    tooltipTr: string;
    badge: FiscalRksvClosingBadgeKind;
};

export function fiscalRksvClosingBadgeLabel(kind: FiscalRksvClosingBadgeKind): string {
    switch (kind) {
        case 'daily':
            return 'Daily';
        case 'monthly':
            return 'Monthly';
        case 'yearly':
            return 'Yearly';
        default:
            return 'One-time';
    }
}

export const FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES: readonly FiscalRksvClosingSidebarLeaf[] = [
    {
        menuKey: '/tagesabschluss',
        href: '/tagesabschluss',
        labelKey: 'nav.fiscalClosingOperative',
        emoji: '🔒',
        accentColor: '#52c41a',
        tooltipTr: 'Günlük kapanış - TSE/RKSV kontrolü. Her gün iş bitiminde kullanılır.',
        badge: 'daily',
    },
    {
        menuKey: '/reporting/tagesbericht',
        href: '/reporting/tagesbericht',
        labelKey: 'nav.tagesbericht',
        emoji: '📄',
        accentColor: '#1890ff',
        tooltipTr: "Resmi günlük RKSV raporu. Operativer Tagesabschluss'tan SONRA kullanılır.",
        badge: 'daily',
    },
    {
        menuKey: '/reporting/monatsbericht',
        href: '/reporting/monatsbericht',
        labelKey: 'nav.monatsbericht',
        emoji: '📅',
        accentColor: '#1890ff',
        tooltipTr: 'Resmi aylık RKSV raporu. Ayın son iş gününde kullanılır.',
        badge: 'monthly',
    },
    {
        menuKey: '/reporting/jahresbericht',
        href: '/reporting/jahresbericht',
        labelKey: 'nav.jahresbericht',
        emoji: '📆',
        accentColor: '#1890ff',
        tooltipTr: 'Resmi yıllık RKSV raporu. Aralık ayında yıl sonunda kullanılır.',
        badge: 'yearly',
    },
    {
        menuKey: '/rksv/sb/startbeleg',
        href: '/rksv/sonderbelege?focus=startbeleg',
        labelKey: 'nav.fiscalClosingStartbeleg',
        emoji: '🏁',
        accentColor: '#fa8c16',
        tooltipTr: 'İlk başlangıç fişi. Yeni kasa açıldığında bir kere kullanılır.',
        badge: 'one-time',
    },
    {
        menuKey: '/rksv/sb/schlussbeleg',
        href: '/rksv/sonderbelege?focus=schlussbeleg',
        labelKey: 'nav.fiscalClosingSchlussbeleg',
        emoji: '🚪',
        accentColor: '#f5222d',
        tooltipTr: 'Kasa kapatma fişi. Kasa kalıcı olarak kullanımdan kaldırılacağında kullanılır.',
        badge: 'one-time',
    },
    {
        menuKey: '/rksv/sonderbelege',
        href: '/rksv/sonderbelege',
        labelKey: 'nav.fiscalClosingSonderbelege',
        emoji: '🧪',
        accentColor: '#722ed1',
        tooltipTr: 'Özel RKSV fişleri. Yedekleme, test veya özel durumlar için.',
        badge: 'one-time',
    },
];
