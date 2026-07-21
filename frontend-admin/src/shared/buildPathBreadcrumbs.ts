import {
  ADMIN_OVERVIEW_BREADCRUMB_KEY,
  ADMIN_OVERVIEW_HREF,
  type AdminBreadcrumbItem,
} from '@/shared/adminShellLabels';
import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';

const UUID_SEGMENT = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Parent / structural segments that are not always sidebar leaves. */
const SEGMENT_LABEL_KEYS: Record<string, string> = {
  admin: 'common.breadcrumb.admin',
  settings: 'nav.settingsHub',
  tenant: 'common.breadcrumb.tenant',
  tenants: 'common.breadcrumb.tenants',
  orders: 'common.breadcrumb.orders',
  digital: 'common.breadcrumb.digital',
  profile: 'common.breadcrumb.profile',
  reporting: 'nav.reporting',
  payments: 'nav.payments',
  backup: 'nav.backupDr',
  rksv: 'nav.rksvOperationsOverview',
  staff: 'nav.staff',
  users: 'nav.users',
};

function normalizeHref(href: string): string {
  return href.split('?')[0]?.split('#')[0] || href;
}

/** href → i18n labelKey from sidebar catalog (longest exact paths preferred by callers). */
export function buildNavHrefLabelKeyMap(): Map<string, string> {
  const map = new Map<string, string>();
  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    map.set(normalizeHref(item.href), item.labelKey);
  }
  return map;
}

function humanizeSegment(segment: string): string {
  return segment
    .split('-')
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function resolveSegmentLabel(
  segment: string,
  href: string,
  hrefLabelKeys: Map<string, string>,
  t: (key: string) => string
): string {
  const catalogKey = hrefLabelKeys.get(href);
  if (catalogKey) return t(catalogKey);

  if (UUID_SEGMENT.test(segment)) {
    return t('common.breadcrumb.detail');
  }

  const segmentKey = SEGMENT_LABEL_KEYS[segment.toLowerCase()];
  if (segmentKey) return t(segmentKey);

  return humanizeSegment(segment);
}

/**
 * Derive breadcrumb trail from a pathname using sidebar catalog + segment fallbacks.
 * Always starts with Übersicht (`/dashboard`).
 */
export function buildPathBreadcrumbs(
  pathname: string,
  t: (key: string) => string
): AdminBreadcrumbItem[] {
  const raw = (pathname || '/').split('/').filter(Boolean);
  const hrefLabelKeys = buildNavHrefLabelKeyMap();

  const overview: AdminBreadcrumbItem = {
    title: t(ADMIN_OVERVIEW_BREADCRUMB_KEY),
    href: ADMIN_OVERVIEW_HREF,
  };

  if (raw.length === 0 || (raw.length === 1 && raw[0] === 'dashboard')) {
    return [{ title: overview.title }];
  }

  const items: AdminBreadcrumbItem[] = [overview];

  raw.forEach((segment, index) => {
    if (index === 0 && segment === 'dashboard') return;

    const href = '/' + raw.slice(0, index + 1).join('/');
    const isLast = index === raw.length - 1;
    const title = resolveSegmentLabel(segment, href, hrefLabelKeys, t);

    items.push(isLast ? { title } : { title, href });
  });

  return items;
}
