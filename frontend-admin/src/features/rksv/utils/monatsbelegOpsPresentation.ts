import type { MonatsbelegBlockingMode } from '@/features/settings/api/monatsbelegPolicy';
import type { MonatsbelegListRow } from '@/features/rksv/api/monatsbelege';
import type { PastMissingMonatsbelegEntry } from '@/features/rksv/utils/monatsbelegMissingMonths';
import {
  formatViennaYearMonth,
  getMonthDifference,
  getViennaCalendarDate,
} from '@/shared/utils/viennaCalendar';

/** Product catch-up window for Auto-Monatsbeleg (Vienna calendar day 1–14). */
export const AUTO_MONATSBELEG_CATCH_UP_THROUGH_DAY = 14;

export const JAHRESBELEG_FON_DEADLINE_MONTH = 2;
export const JAHRESBELEG_FON_DEADLINE_DAY = 15;

export type MonatsbelegOpsDisplayStatus = 'Created' | 'Missing' | 'AutoCreated' | 'Overdue';

export type MonatsbelegAutoCreateDisplayStatus =
  | 'Pending'
  | 'Success'
  | 'Failed'
  | 'Missed'
  | 'None';

export type MonatsbelegOpsRow = MonatsbelegListRow & {
  displayStatus: MonatsbelegOpsDisplayStatus;
  autoCreateStatus: MonatsbelegAutoCreateDisplayStatus;
  salesGateMode: MonatsbelegBlockingMode;
  isSyntheticMissing: boolean;
};

export type MonatsbelegOpsRegisterLookup = {
  id: string;
  registerNumber: string;
  location?: string | null;
};

function rowKey(cashRegisterId: string, year: number, month: number): string {
  return `${cashRegisterId}:${year}:${month}`;
}

function isWindowClosed(lastError?: string | null): boolean {
  return (lastError ?? '').trim().toLowerCase() === 'window_closed';
}

export function isInAutoMonatsbelegCatchUpWindow(now: Date = new Date()): boolean {
  const { day } = getViennaCalendarDate(now);
  return day >= 1 && day <= AUTO_MONATSBELEG_CATCH_UP_THROUGH_DAY;
}

export function getJahresbelegFonDeadline(jahresbelegYear: number): {
  year: number;
  month: number;
  day: number;
} {
  return {
    year: jahresbelegYear + 1,
    month: JAHRESBELEG_FON_DEADLINE_MONTH,
    day: JAHRESBELEG_FON_DEADLINE_DAY,
  };
}

/** `15.02.2027` for a 2026 Jahresbeleg (FA display, not a legal claim). */
export function formatJahresbelegFonDeadline(jahresbelegYear: number): string {
  const d = getJahresbelegFonDeadline(jahresbelegYear);
  return `${String(d.day).padStart(2, '0')}.${String(d.month).padStart(2, '0')}.${d.year}`;
}

export function isJahresbelegFonDeadlinePassed(
  jahresbelegYear: number,
  now: Date = new Date()
): boolean {
  const vienna = getViennaCalendarDate(now);
  const d = getJahresbelegFonDeadline(jahresbelegYear);
  const today = vienna.year * 10_000 + vienna.month * 100 + vienna.day;
  const deadline = d.year * 10_000 + d.month * 100 + d.day;
  return today > deadline;
}

export function jahresbelegNeedsFonAttention(row: Pick<MonatsbelegOpsRow, 'fonStatus' | 'displayStatus'>): boolean {
  if (row.displayStatus === 'Missing' || row.displayStatus === 'Overdue') return true;
  const fon = (row.fonStatus ?? '').trim();
  return fon !== 'Verified' && fon !== 'NotRequired';
}

export function resolveOpsDisplayStatus(input: {
  listStatus?: string | null;
  autoCreated?: boolean;
  isOverdue?: boolean;
  lastError?: string | null;
}): MonatsbelegOpsDisplayStatus {
  const status = (input.listStatus ?? '').trim().toLowerCase();
  if (status === 'created') {
    return input.autoCreated ? 'AutoCreated' : 'Created';
  }
  if (input.isOverdue || isWindowClosed(input.lastError)) {
    return 'Overdue';
  }
  return 'Missing';
}

export function resolveAutoCreateStatus(input: {
  listStatus?: string | null;
  autoCreated?: boolean;
  isOverdue?: boolean;
  lastError?: string | null;
  autoEnabled: boolean;
  monthDiff: number;
  now?: Date;
}): MonatsbelegAutoCreateDisplayStatus {
  const status = (input.listStatus ?? '').trim().toLowerCase();
  if (status === 'created') {
    return input.autoCreated ? 'Success' : 'None';
  }
  if (isWindowClosed(input.lastError)) {
    return 'Missed';
  }
  if (status === 'failed') {
    return 'Failed';
  }
  if (!input.autoEnabled) {
    return 'None';
  }
  const now = input.now ?? new Date();
  if (input.monthDiff === 1 && isInAutoMonatsbelegCatchUpWindow(now)) {
    return 'Pending';
  }
  return 'Missed';
}

function emptyListRow(partial: {
  cashRegisterId: string;
  registerNumber: string;
  registerLocation?: string | null;
  year: number;
  month: number;
}): MonatsbelegListRow {
  const isJahresbeleg = partial.month === 12;
  return {
    paymentId: null,
    cashRegisterId: partial.cashRegisterId,
    registerNumber: partial.registerNumber,
    registerLocation: partial.registerLocation ?? null,
    year: partial.year,
    month: partial.month,
    period: formatViennaYearMonth(partial.year, partial.month),
    createdAtUtc: null,
    createdBy: '—',
    createdByUserId: '',
    tseSignature: '',
    depStatus: 'Missing',
    fonStatus: isJahresbeleg ? 'Pending' : 'NotRequired',
    isJahresbeleg,
    autoCreated: false,
    status: 'missing',
    lastError: null,
    attemptCount: 0,
    correlationId: null,
  };
}

export function buildMonatsbelegOpsRows(args: {
  items: MonatsbelegListRow[];
  missing: PastMissingMonatsbelegEntry[];
  year: number;
  cashRegisterId?: string;
  registers: MonatsbelegOpsRegisterLookup[];
  autoEnabled: boolean;
  salesGateMode: MonatsbelegBlockingMode;
  now?: Date;
}): MonatsbelegOpsRow[] {
  const now = args.now ?? new Date();
  const registerById = new Map(args.registers.map((r) => [r.id, r]));
  const missingByKey = new Map<string, PastMissingMonatsbelegEntry>();
  for (const entry of args.missing) {
    if (entry.year !== args.year) continue;
    if (args.cashRegisterId && entry.cashRegisterId !== args.cashRegisterId) continue;
    missingByKey.set(rowKey(entry.cashRegisterId, entry.year, entry.month), entry);
  }

  const covered = new Set<string>();
  const rows: MonatsbelegOpsRow[] = [];

  for (const item of args.items) {
    if (item.year !== args.year) continue;
    if (args.cashRegisterId && item.cashRegisterId !== args.cashRegisterId) continue;
    const key = rowKey(item.cashRegisterId, item.year, item.month);
    covered.add(key);
    const missing = missingByKey.get(key);
    const monthDiff = getMonthDifference(item.year, item.month, now);
    const isOverdue = Boolean(missing?.isOverdue) || isWindowClosed(item.lastError);
    rows.push({
      ...item,
      displayStatus: resolveOpsDisplayStatus({
        listStatus: item.status,
        autoCreated: item.autoCreated,
        isOverdue,
        lastError: item.lastError,
      }),
      autoCreateStatus: resolveAutoCreateStatus({
        listStatus: item.status,
        autoCreated: item.autoCreated,
        isOverdue,
        lastError: item.lastError,
        autoEnabled: args.autoEnabled,
        monthDiff,
        now,
      }),
      salesGateMode: args.salesGateMode,
      isSyntheticMissing: false,
    });
  }

  for (const entry of missingByKey.values()) {
    const key = rowKey(entry.cashRegisterId, entry.year, entry.month);
    if (covered.has(key)) continue;
    const lookup = registerById.get(entry.cashRegisterId);
    const monthDiff = getMonthDifference(entry.year, entry.month, now);
    const base = emptyListRow({
      cashRegisterId: entry.cashRegisterId,
      registerNumber: lookup?.registerNumber ?? entry.cashRegisterId.slice(0, 8),
      registerLocation: lookup?.location ?? null,
      year: entry.year,
      month: entry.month,
    });
    rows.push({
      ...base,
      displayStatus: resolveOpsDisplayStatus({
        listStatus: 'missing',
        isOverdue: entry.isOverdue,
      }),
      autoCreateStatus: resolveAutoCreateStatus({
        listStatus: 'missing',
        isOverdue: entry.isOverdue,
        autoEnabled: args.autoEnabled,
        monthDiff,
        now,
      }),
      salesGateMode: args.salesGateMode,
      isSyntheticMissing: true,
    });
  }

  rows.sort((a, b) => {
    if (a.year !== b.year) return a.year - b.year;
    if (a.month !== b.month) return a.month - b.month;
    return a.registerNumber.localeCompare(b.registerNumber);
  });
  return rows;
}

export function filterMonatsbelegOpsRows(
  rows: MonatsbelegOpsRow[],
  filter: string
): MonatsbelegOpsRow[] {
  switch (filter) {
    case 'created':
      return rows.filter((r) => r.displayStatus === 'Created');
    case 'failed':
      return rows.filter((r) => r.status === 'failed' || r.autoCreateStatus === 'Failed');
    case 'autoCreated':
      return rows.filter((r) => r.displayStatus === 'AutoCreated');
    case 'missing':
      return rows.filter((r) => r.displayStatus === 'Missing');
    case 'overdue':
      return rows.filter((r) => r.displayStatus === 'Overdue');
    case 'fonPending':
      return rows.filter((r) => {
        const fon = (r.fonStatus ?? '').trim();
        return fon === 'Pending' || fon === 'Submitted' || fon === 'ManualVerificationRequired';
      });
    default:
      return rows;
  }
}

export function isJahresbelegOpsRow(row: Pick<MonatsbelegOpsRow, 'isJahresbeleg' | 'month'>): boolean {
  return row.isJahresbeleg || row.month === 12;
}
