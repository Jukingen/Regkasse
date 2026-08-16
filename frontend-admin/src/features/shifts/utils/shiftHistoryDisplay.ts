/** Stable Identity user id from shift overview (`cashierId`). */
export function shortUserId(userId: string | null | undefined): string {
  if (!userId) return '—';
  const compact = userId.replace(/-/g, '');
  return compact.length <= 8 ? compact : compact.slice(0, 8);
}

export function cashierInitial(name: string | null | undefined): string {
  const trimmed = (name ?? '').trim();
  return trimmed ? trimmed.charAt(0).toUpperCase() : '?';
}

/** Ant Design Tag color for shift status. */
export function shiftStatusTagColor(status: string): string {
  switch (status) {
    case 'Completed':
      return 'success';
    case 'Discrepancy':
      return 'warning';
    case 'Active':
    case 'RegisterOpen':
      return 'error';
    default:
      return 'default';
  }
}

/** Subtle row background for history status (light theme). */
export function shiftStatusRowBackground(status: string): string | undefined {
  switch (status) {
    case 'Completed':
      return '#f6ffed';
    case 'Discrepancy':
      return '#fffbe6';
    case 'Active':
    case 'RegisterOpen':
      return '#fff2f0';
    default:
      return undefined;
  }
}

/** Difference amount text color. */
export function differenceTextColor(difference: number): string {
  if (difference > 0) return '#389e0d';
  if (difference < 0) return '#cf1322';
  return '#8c8c8c';
}

export type ShiftHistoryStatusFilter = 'all' | 'Completed' | 'Discrepancy' | 'Active' | 'RegisterOpen';

export type ShiftHistoryClientFilters = {
  cashierId?: string;
  cashRegisterId?: string;
  status?: ShiftHistoryStatusFilter;
  search?: string;
};

export function filterShiftHistory(
  rows: ReadonlyArray<{
    cashierId: string;
    cashierName: string;
    cashRegisterId: string;
    registerNumber?: string | null;
    status: string;
  }>,
  filters: ShiftHistoryClientFilters
): typeof rows {
  const search = filters.search?.trim().toLowerCase();
  return rows.filter((row) => {
    if (filters.cashierId && row.cashierId !== filters.cashierId) return false;
    if (filters.cashRegisterId && row.cashRegisterId !== filters.cashRegisterId) return false;
    if (filters.status && filters.status !== 'all' && row.status !== filters.status) return false;
    if (search) {
      const register = (row.registerNumber ?? row.cashRegisterId).toLowerCase();
      const name = (row.cashierName ?? '').toLowerCase();
      if (!register.includes(search) && !name.includes(search)) return false;
    }
    return true;
  });
}

export type ShiftHistorySummary = {
  count: number;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  totalDifference: number;
};

export function summarizeShiftHistory(
  rows: ReadonlyArray<{
    totalSales: number;
    totalCash: number;
    totalCard: number;
    difference: number;
  }>
): ShiftHistorySummary {
  return rows.reduce<ShiftHistorySummary>(
    (acc, row) => {
      acc.count += 1;
      acc.totalSales += row.totalSales ?? 0;
      acc.totalCash += row.totalCash ?? 0;
      acc.totalCard += row.totalCard ?? 0;
      acc.totalDifference += row.difference ?? 0;
      return acc;
    },
    { count: 0, totalSales: 0, totalCash: 0, totalCard: 0, totalDifference: 0 }
  );
}

export type ShiftHistoryRegisterGroup<T> = {
  cashRegisterId: string;
  registerLabel: string;
  shifts: T[];
};

export function groupShiftHistoryByRegister<
  T extends { cashRegisterId: string; registerNumber?: string | null; startedAt: string },
>(rows: readonly T[]): ShiftHistoryRegisterGroup<T>[] {
  const map = new Map<string, ShiftHistoryRegisterGroup<T>>();
  for (const row of rows) {
    const existing = map.get(row.cashRegisterId);
    if (existing) {
      existing.shifts.push(row);
    } else {
      map.set(row.cashRegisterId, {
        cashRegisterId: row.cashRegisterId,
        registerLabel: row.registerNumber?.trim() || row.cashRegisterId,
        shifts: [row],
      });
    }
  }
  return Array.from(map.values()).sort((a, b) => a.registerLabel.localeCompare(b.registerLabel));
}
