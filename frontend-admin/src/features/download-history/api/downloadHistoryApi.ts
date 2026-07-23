import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DownloadHistoryListItem = {
  id: string;
  fileName: string;
  fileType: string;
  fileSize: number | null;
  downloadUrl: string | null;
  downloadedAt: string;
  userId: string;
  ipAddress: string | null;
  userAgent: string | null;
  sourceKind: string | null;
  sourceId: string | null;
  canRedownload: boolean;
};

export type DownloadHistoryListResponse = {
  items: DownloadHistoryListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type DownloadHistoryStats = {
  fileCount: number;
  totalBytes: number;
  retentionDays: number;
};

export type DownloadHistoryCleanupResult = {
  deletedCount: number;
  retentionDays: number;
};

export type RecordDownloadHistoryInput = {
  fileName: string;
  fileType: string;
  fileSize?: number | null;
  durationMs?: number | null;
  downloadUrl?: string | null;
  sourceKind?: string | null;
  sourceId?: string | null;
};

export type DownloadHistoryListParams = {
  page?: number;
  pageSize?: number;
  fileType?: string;
  sourceKind?: string;
  q?: string;
  fromUtc?: string;
  toUtc?: string;
};

export const downloadHistoryQueryKey = (params: DownloadHistoryListParams) =>
  [
    'admin',
    'download-history',
    params.page ?? 1,
    params.pageSize ?? 20,
    params.fileType ?? '',
    params.sourceKind ?? '',
    params.q ?? '',
    params.fromUtc ?? '',
    params.toUtc ?? '',
  ] as const;

export const downloadHistoryStatsQueryKey = ['admin', 'download-history', 'stats'] as const;

export async function fetchDownloadHistory(
  params: DownloadHistoryListParams
): Promise<DownloadHistoryListResponse> {
  const response = await AXIOS_INSTANCE.get<DownloadHistoryListResponse>(
    '/api/admin/download-history',
    {
      params: {
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        fileType: params.fileType || undefined,
        sourceKind: params.sourceKind || undefined,
        q: params.q || undefined,
        fromUtc: params.fromUtc || undefined,
        toUtc: params.toUtc || undefined,
      },
    }
  );
  return response.data;
}

export async function fetchDownloadHistoryStats(): Promise<DownloadHistoryStats> {
  const response = await AXIOS_INSTANCE.get<DownloadHistoryStats>(
    '/api/admin/download-history/stats',
    { params: { mineOnly: true } }
  );
  return response.data;
}

export async function cleanupOldDownloadHistory(): Promise<DownloadHistoryCleanupResult> {
  const response = await AXIOS_INSTANCE.post<DownloadHistoryCleanupResult>(
    '/api/admin/download-history/cleanup-old'
  );
  return response.data;
}

export async function recordDownloadHistory(
  body: RecordDownloadHistoryInput
): Promise<DownloadHistoryListItem> {
  const response = await AXIOS_INSTANCE.post<DownloadHistoryListItem>(
    '/api/admin/download-history',
    body
  );
  return response.data;
}

export async function fetchRedownloadBlob(id: string): Promise<Blob> {
  const response = await AXIOS_INSTANCE.get<Blob>(`/api/admin/download-history/${id}/redownload`, {
    responseType: 'blob',
  });
  return new Blob([response.data], {
    type: response.headers['content-type'] || response.data.type || 'application/octet-stream',
  });
}

export async function redownloadFromHistory(id: string, fileName: string): Promise<void> {
  const blob = await fetchRedownloadBlob(id);
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = globalThis.document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}

export function useDownloadHistory(
  params: DownloadHistoryListParams,
  options?: { enabled?: boolean }
) {
  return useQuery({
    queryKey: downloadHistoryQueryKey(params),
    queryFn: () => fetchDownloadHistory(params),
    staleTime: 15_000,
    enabled: options?.enabled !== false,
  });
}

export function useDownloadHistoryStats() {
  return useQuery({
    queryKey: downloadHistoryStatsQueryKey,
    queryFn: fetchDownloadHistoryStats,
    staleTime: 30_000,
  });
}

export function useCleanupOldDownloadHistory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: cleanupOldDownloadHistory,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'download-history'] });
    },
  });
}
