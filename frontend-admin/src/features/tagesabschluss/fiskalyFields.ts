export type TagesabschlussFiskalyFields = {
  fiskalyStatus?: string | null;
  fiskalyReceiptId?: string | null;
  fiskalyError?: string | null;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

export function readTagesabschlussFiskalyFields(row: unknown): TagesabschlussFiskalyFields {
  if (!isRecord(row)) return {};
  return {
    fiskalyStatus: typeof row.fiskalyStatus === 'string' ? row.fiskalyStatus : null,
    fiskalyReceiptId: typeof row.fiskalyReceiptId === 'string' ? row.fiskalyReceiptId : null,
    fiskalyError: typeof row.fiskalyError === 'string' ? row.fiskalyError : null,
  };
}

export function fiskalyStatusColor(status: string | null | undefined): string {
  switch ((status ?? '').toLowerCase()) {
    case 'submitted':
      return 'success';
    case 'failed':
      return 'error';
    case 'pending':
      return 'processing';
    case 'skipped':
      return 'default';
    default:
      return 'default';
  }
}
