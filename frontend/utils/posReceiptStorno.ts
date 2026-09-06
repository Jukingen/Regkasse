import { hasPermission, type PosPermissionUser } from './posPermissions';

export const POS_RECEIPT_STORNO_STATUSES = {
  PAID: 'Paid',
  STORNO: 'Storno',
  REFUND: 'Refund',
} as const;

export function hasPosReceiptStornoPermission(
  user: PosPermissionUser | null | undefined
): boolean {
  return hasPermission(user, 'payment.cancel') || hasPermission(user, 'refund.create');
}

export function isViennaCalendarToday(iso: string, now: Date = new Date()): boolean {
  const issued = new Date(iso);
  if (Number.isNaN(issued.getTime())) return false;
  const fmt = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Vienna',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
  return fmt.format(issued) === fmt.format(now);
}

export function isReceiptStatusStornoable(status: string): boolean {
  return status === POS_RECEIPT_STORNO_STATUSES.PAID;
}

export type PosReceiptStornoRow = {
  status: string;
  grandTotal: number;
  cashierId?: string | null;
  issuedAt: string;
};

export type PosReceiptStornoActor = PosPermissionUser & {
  id?: string;
};

export function canShowReceiptStornoButton(
  row: PosReceiptStornoRow,
  actor: PosReceiptStornoActor | null | undefined
): boolean {
  if (!hasPosReceiptStornoPermission(actor)) return false;
  if (!isReceiptStatusStornoable(row.status)) return false;
  if (!(row.grandTotal > 0)) return false;

  const role = actor?.role ?? '';
  const roles = actor?.roles ?? [];
  const isPrivileged =
    role === 'SuperAdmin' ||
    role === 'Manager' ||
    roles.includes('SuperAdmin') ||
    roles.includes('Manager');
  if (isPrivileged || hasPermission(actor, 'system.critical')) return true;

  if (!actor?.id || !row.cashierId) return false;
  if (actor.id.trim().toLowerCase() !== row.cashierId.trim().toLowerCase()) return false;
  return isViennaCalendarToday(row.issuedAt);
}
