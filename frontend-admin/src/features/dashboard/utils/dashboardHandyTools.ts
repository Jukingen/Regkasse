/**
 * Curated dashboard shortcuts (permission-filtered at render).
 * Super Admin platform tools sit first; tenant ops follow.
 */
export const DASHBOARD_HANDY_TOOL_HREFS = [
  '/admin/cash-registers',
  '/kassenverwaltung',
  '/admin/tenants',
  '/tagesabschluss',
  '/rksv/status',
  '/shifts',
  '/receipts',
  '/backup',
  '/admin/users',
] as const;

export function filterAccessibleHandyToolHrefs(
  hrefs: readonly string[],
  canAccess: (path: string) => boolean
): string[] {
  return hrefs.filter((href) => canAccess(href));
}
