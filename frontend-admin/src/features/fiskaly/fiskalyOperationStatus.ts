export type FiskalyLiveStatus = 'Pending' | 'Processing' | 'Success' | 'Failed';

export type FiskalyOperationStatusEvent = {
  id: string;
  tenantId: string;
  operationType: string;
  status: string;
  progressPercent: number;
  cashRegisterId: string;
  cashRegisterName?: string | null;
  receiptNumber?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
};

export const FISKALY_STATUS_POLL_MS = 3000;

export const FISKALY_OPERATION_STATUS_HUB_PATH = '/hubs/fiskaly-operation-status';

export function isFiskalyHistoryInFlight(status: string | null | undefined): boolean {
  return status === 'Pending' || status === 'Processing';
}

export function isFiskalyHistoryTerminal(status: string | null | undefined): boolean {
  return status === 'Success' || status === 'Failed';
}

export function canRetryFiskalyHistoryStatus(status: string | null | undefined): boolean {
  return status === 'Failed' || status === 'Pending';
}

export function fiskalyHistoryProgressPercent(
  status: string | null | undefined,
  explicit?: number | null
): number {
  if (typeof explicit === 'number' && Number.isFinite(explicit)) {
    return Math.min(100, Math.max(0, Math.round(explicit)));
  }
  switch (status) {
    case 'Pending':
      return 5;
    case 'Processing':
      return 55;
    case 'Success':
    case 'Failed':
      return 100;
    default:
      return 0;
  }
}

export function normalizeFiskalyLiveStatus(status: string | null | undefined): FiskalyLiveStatus {
  if (status === 'Processing') return 'Processing';
  if (status === 'Failed') return 'Failed';
  if (status === 'Success' || status === 'Completed') return 'Success';
  return 'Pending';
}
