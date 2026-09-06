import { customInstance } from '@/lib/axios';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type FiskalyErrorReviewStatus = 'open' | 'resolved' | 'known_issue';

export type FiskalyErrorQuery = {
  fromUtc?: string;
  toUtc?: string;
  operationType?: string;
  tenantId?: string;
  reviewStatus?: FiskalyErrorReviewStatus;
  errorCode?: string;
  search?: string;
  page?: number;
  pageSize?: number;
};

export type FiskalyKnownErrorSolution = {
  code: string;
  title: string;
  hint: string;
};

export type FiskalyErrorKpis = {
  totalErrors: number;
  errorRatePercent: number;
  totalOperations: number;
  mostCommonErrorCode?: string | null;
  mostCommonErrorCount: number;
  tenantWithMostErrorsId?: string | null;
  tenantWithMostErrorsName?: string | null;
  tenantWithMostErrorsCount: number;
  openCount: number;
  resolvedCount: number;
  knownIssueCount: number;
};

export type FiskalyErrorCount = {
  key: string;
  count: number;
  sampleMessage?: string | null;
};

export type FiskalyErrorTenantCount = {
  tenantId: string;
  tenantName?: string | null;
  count: number;
};

export type FiskalyErrorDailyPoint = {
  date: string;
  count: number;
};

export type FiskalyErrorWeeklyPoint = {
  week: string;
  weekStart: string;
  count: number;
};

export type FiskalyErrorListItem = {
  id: string;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  operationType: string;
  status: string;
  errorCode?: string | null;
  errorMessage?: string | null;
  tenantId: string;
  tenantName?: string | null;
  userId: string;
  userDisplayName?: string | null;
  cashRegisterId: string;
  receiptNumber?: string | null;
  cashRegisterName?: string | null;
  reviewStatus: FiskalyErrorReviewStatus | string;
  reviewedAtUtc?: string | null;
  reviewedByUserId?: string | null;
  knownSolution?: FiskalyKnownErrorSolution | null;
};

export type FiskalyErrorDetail = FiskalyErrorListItem & {
  receiptId?: string | null;
  requestPayloadJson?: string | null;
  responsePayloadJson?: string | null;
  stackTrace?: string | null;
};

export type FiskalyErrorPaged = {
  items: FiskalyErrorListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type FiskalyErrorStats = {
  fromUtc: string;
  toUtc: string;
  tenantId?: string | null;
  kpis: FiskalyErrorKpis;
  daily: FiskalyErrorDailyPoint[];
  weekly: FiskalyErrorWeeklyPoint[];
  topErrors: FiskalyErrorCount[];
  byOperationType: FiskalyErrorCount[];
  byTenant: FiskalyErrorTenantCount[];
};

export type FiskalyErrorExport = {
  blob: Blob;
  fileName: string;
};

const BASE = '/api/admin/fiskaly/errors';

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object' && !Array.isArray(value);
}

async function rethrowBlobError(error: unknown): Promise<never> {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data;
  if (data instanceof Blob) {
    const text = await data.text();
    try {
      const json: unknown = JSON.parse(text);
      throw Object.assign(
        new Error(isRecord(json) && typeof json.message === 'string' ? json.message : text),
        { response: { data: json } }
      );
    } catch (parsed) {
      if (parsed instanceof SyntaxError) throw error;
      throw parsed;
    }
  }
  throw error;
}

export async function getFiskalyErrors(
  params: FiskalyErrorQuery,
  signal?: AbortSignal
): Promise<FiskalyErrorPaged> {
  return customInstance<FiskalyErrorPaged>({
    url: BASE,
    method: 'GET',
    params,
    signal,
  });
}

export async function getFiskalyErrorStats(
  params: Pick<FiskalyErrorQuery, 'fromUtc' | 'toUtc' | 'operationType' | 'tenantId'>,
  signal?: AbortSignal
): Promise<FiskalyErrorStats> {
  return customInstance<FiskalyErrorStats>({
    url: `${BASE}/stats`,
    method: 'GET',
    params,
    signal,
  });
}

export async function getFiskalyErrorById(
  id: string,
  signal?: AbortSignal
): Promise<FiskalyErrorDetail> {
  return customInstance<FiskalyErrorDetail>({
    url: `${BASE}/${id}`,
    method: 'GET',
    signal,
  });
}

export async function exportFiskalyErrors(
  params: Omit<FiskalyErrorQuery, 'page' | 'pageSize'> & { format?: 'csv' | 'pdf' },
  signal?: AbortSignal
): Promise<FiskalyErrorExport> {
  try {
    const response = await AXIOS_INSTANCE.get(`${BASE}/export`, {
      params,
      responseType: 'blob',
      signal,
    });
    const blob = response.data as Blob;
    const disposition = String(response.headers['content-disposition'] ?? '');
    const match = /filename="?([^"]+)"?/i.exec(disposition);
    const fileName =
      match?.[1] ?? `fiskaly-errors.${params.format === 'pdf' ? 'pdf' : 'csv'}`;
    return { blob, fileName };
  } catch (err) {
    return await rethrowBlobError(err);
  }
}

export async function setFiskalyErrorReview(
  id: string,
  reviewStatus: FiskalyErrorReviewStatus,
  signal?: AbortSignal
): Promise<FiskalyErrorListItem> {
  return customInstance<FiskalyErrorListItem>({
    url: `${BASE}/${id}/resolve`,
    method: 'PUT',
    data: { reviewStatus },
    signal,
  });
}
