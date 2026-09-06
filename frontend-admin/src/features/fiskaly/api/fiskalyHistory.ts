import { customInstance } from '@/lib/axios';
import { parseFiskalyReceiptError, type FiskalyReceiptEnvelope } from '@/features/fiskaly/api/fiskalyReceipts';

export type FiskalyHistoryStatus = 'Success' | 'Failed' | 'Pending' | 'Processing';

export type FiskalyHistoryListItem = {
  id: string;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  operationType: string;
  status: string;
  progressPercent?: number;
  cashRegisterId: string;
  cashRegisterName?: string | null;
  receiptNumber?: string | null;
  receiptId?: string | null;
  userId: string;
  userDisplayName?: string | null;
  tenantId: string;
  tenantName?: string | null;
  retriedFromId?: string | null;
  retryCount: number;
  errorCode?: string | null;
  errorMessage?: string | null;
};

export type FiskalyHistoryDetail = FiskalyHistoryListItem & {
  requestPayloadJson?: string | null;
  responsePayloadJson?: string | null;
};

export type FiskalyHistoryPaged = {
  items: FiskalyHistoryListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type FiskalyHistoryQuery = {
  fromUtc?: string;
  toUtc?: string;
  operationType?: string;
  status?: string;
  search?: string;
  tenantId?: string;
  page?: number;
  pageSize?: number;
};

export type FiskalyHistoryRetryResult = {
  operation: FiskalyReceiptEnvelope;
  history?: FiskalyHistoryDetail | null;
};

const BASE = '/api/admin/fiskaly/history';

export async function getFiskalyHistory(
  params: FiskalyHistoryQuery,
  signal?: AbortSignal
): Promise<FiskalyHistoryPaged> {
  return customInstance<FiskalyHistoryPaged>({
    url: BASE,
    method: 'GET',
    params,
    signal,
  });
}

export async function getFiskalyHistoryById(
  id: string,
  signal?: AbortSignal
): Promise<FiskalyHistoryDetail> {
  return customInstance<FiskalyHistoryDetail>({
    url: `${BASE}/${id}`,
    method: 'GET',
    signal,
  });
}

export async function retryFiskalyHistory(
  id: string,
  signal?: AbortSignal
): Promise<FiskalyHistoryRetryResult> {
  try {
    return await customInstance<FiskalyHistoryRetryResult>({
      url: `${BASE}/${id}/retry`,
      method: 'POST',
      signal,
    });
  } catch (err) {
    const parsed = parseFiskalyReceiptError(err);
    if (parsed) {
      return { operation: { success: false, error: parsed } };
    }
    throw err;
  }
}

export async function fetchAllFiskalyHistory(
  params: Omit<FiskalyHistoryQuery, 'page' | 'pageSize'>,
  maxRows = 2000,
  signal?: AbortSignal
): Promise<FiskalyHistoryListItem[]> {
  const pageSize = 100;
  const rows: FiskalyHistoryListItem[] = [];
  let page = 1;
  while (rows.length < maxRows) {
    const batch = await getFiskalyHistory({ ...params, page, pageSize }, signal);
    rows.push(...(batch.items ?? []));
    if (!batch.items?.length || rows.length >= batch.totalCount || page >= batch.totalPages) {
      break;
    }
    page += 1;
  }
  return rows.slice(0, maxRows);
}
