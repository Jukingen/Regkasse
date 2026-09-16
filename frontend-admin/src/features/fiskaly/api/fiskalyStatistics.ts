import { customInstance } from '@/lib/axios';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type FiskalyStatisticsQuery = {
  fromUtc?: string;
  toUtc?: string;
  operationType?: string;
  tenantId?: string;
};

export type FiskalyStatisticsKpis = {
  totalOperations: number;
  successRatePercent: number;
  mostUsedOperationType?: string | null;
  averageProcessingTimeMs?: number | null;
  totalErrors: number;
  successCount: number;
  failedCount: number;
  inFlightCount: number;
};

export type FiskalyStatisticsCount = {
  key: string;
  count: number;
};

export type FiskalyStatisticsDailyPoint = {
  date: string;
  total: number;
  success: number;
  failed: number;
};

export type FiskalyStatisticsMonthlyPoint = {
  yearMonth: string;
  total: number;
  success: number;
  failed: number;
};

export type FiskalyStatistics = {
  fromUtc: string;
  toUtc: string;
  tenantId?: string | null;
  kpis: FiskalyStatisticsKpis;
  daily: FiskalyStatisticsDailyPoint[];
  byOperationType: FiskalyStatisticsCount[];
  byStatus: FiskalyStatisticsCount[];
  monthly: FiskalyStatisticsMonthlyPoint[];
};

export type FiskalyStatisticsExport = {
  blob: Blob;
  fileName: string;
};

const BASE = '/api/admin/fiskaly/statistics';

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

export async function getFiskalyStatistics(
  params: FiskalyStatisticsQuery,
  signal?: AbortSignal
): Promise<FiskalyStatistics> {
  return customInstance<FiskalyStatistics>({
    url: BASE,
    method: 'GET',
    params,
    signal,
  });
}

export async function exportFiskalyStatistics(
  params: FiskalyStatisticsQuery & { format: 'csv' | 'pdf' },
  signal?: AbortSignal
): Promise<FiskalyStatisticsExport> {
  try {
    const response = await AXIOS_INSTANCE.get(BASE + '/export', {
      params,
      responseType: 'blob',
      signal,
    });
    const blob = response.data as Blob;
    const disposition = String(response.headers['content-disposition'] ?? '');
    const match = /filename="?([^"]+)"?/i.exec(disposition);
    const fileName =
      match?.[1] ?? `fiskaly-statistics.${params.format === 'pdf' ? 'pdf' : 'csv'}`;
    return { blob, fileName };
  } catch (err) {
    return await rethrowBlobError(err);
  }
}
