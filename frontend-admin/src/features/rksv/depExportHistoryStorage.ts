const STORAGE_KEY = 'rksv-dep-export-history-v1';
const MAX_ENTRIES = 20;

export type DepExportHistoryEntry = {
  id: string;
  exportedAtUtc: string;
  cashRegisterId: string;
  registerLabel: string;
  fromUtc: string;
  toUtc: string;
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
  groupCount: number;
  totalSignatures: number;
};

function isHistoryEntry(value: unknown): value is DepExportHistoryEntry {
  if (!value || typeof value !== 'object') return false;
  const row = value as DepExportHistoryEntry;
  return (
    typeof row.id === 'string' &&
    typeof row.exportedAtUtc === 'string' &&
    typeof row.cashRegisterId === 'string' &&
    typeof row.registerLabel === 'string' &&
    typeof row.fromUtc === 'string' &&
    typeof row.toUtc === 'string' &&
    typeof row.includeSpecialReceipts === 'boolean' &&
    typeof row.includeDailyClosings === 'boolean' &&
    typeof row.groupCount === 'number' &&
    typeof row.totalSignatures === 'number'
  );
}

export function readDepExportHistory(): DepExportHistoryEntry[] {
  if (typeof globalThis.localStorage === 'undefined') return [];
  try {
    const raw = globalThis.localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isHistoryEntry);
  } catch {
    return [];
  }
}

export function appendDepExportHistory(entry: DepExportHistoryEntry): DepExportHistoryEntry[] {
  const next = [entry, ...readDepExportHistory().filter((row) => row.id !== entry.id)].slice(
    0,
    MAX_ENTRIES
  );
  if (typeof globalThis.localStorage !== 'undefined') {
    globalThis.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  }
  return next;
}

export function clearDepExportHistory(): void {
  if (typeof globalThis.localStorage !== 'undefined') {
    globalThis.localStorage.removeItem(STORAGE_KEY);
  }
}
