/** Canonical FA routes for cash register list, detail, and register-scoped reports. */

export const KASSENVERWALTUNG_PATH = '/kassenverwaltung';
export const ADMIN_CASH_REGISTER_LIST_PATH = '/admin/cash-registers';
export const CASH_REGISTER_REPORTS_PATH = '/admin/reports';

const DETAIL_ID_RE =
  /^\/admin\/cash-registers\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

function normalizePathname(pathname: string): string {
  return pathname.replace(/\/$/, '') || '/';
}

export function cashRegisterDetailPath(id: string): string {
  return `${ADMIN_CASH_REGISTER_LIST_PATH}/${id.trim()}`;
}

export function cashRegisterReportsPath(registerId?: string | null): string {
  const id = registerId?.trim();
  if (!id) {
    return CASH_REGISTER_REPORTS_PATH;
  }
  return `${CASH_REGISTER_REPORTS_PATH}?registerId=${encodeURIComponent(id)}`;
}

export function parseCashRegisterDetailId(pathname: string): string | null {
  const match = DETAIL_ID_RE.exec(normalizePathname(pathname));
  return match?.[1] ?? null;
}

export function isCashRegisterDetailPath(pathname: string): boolean {
  return parseCashRegisterDetailId(pathname) != null;
}
