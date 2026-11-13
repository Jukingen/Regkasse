import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DownloadAnalyticsKindStat = {
  key: string;
  label: string;
  count: number;
  percent: number;
};

export type DownloadAnalyticsUserStat = {
  userId: string;
  displayName: string;
  count: number;
};

export type DownloadAnalyticsTenantStat = {
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  count: number;
  percent: number;
};

export type DownloadAnalyticsTrendPoint = {
  periodKey: string;
  label: string;
  count: number;
  totalBytes: number;
};

export type DownloadAnalyticsSlowExport = {
  id: string;
  fileName: string;
  sourceKind: string | null;
  fileType: string;
  fileSize: number | null;
  durationMs: number | null;
  downloadedAt: string;
  userId: string;
  displayName: string;
  rankBy: string;
};

export type DownloadHistoryAnalytics = {
  totalCount: number;
  todayCount: number;
  monthCount: number;
  totalBytes: number;
  retentionDays: number;
  includesPlatformTenants: boolean;
  topKinds: DownloadAnalyticsKindStat[];
  topUsers: DownloadAnalyticsUserStat[];
  topTenants: DownloadAnalyticsTenantStat[];
  dailyTrend: DownloadAnalyticsTrendPoint[];
  weeklyTrend: DownloadAnalyticsTrendPoint[];
  monthlyTrend: DownloadAnalyticsTrendPoint[];
  slowExports: DownloadAnalyticsSlowExport[];
};

export const downloadHistoryAnalyticsQueryKey = (platform: boolean) =>
  ['admin', 'download-history', 'analytics', platform] as const;

export async function fetchDownloadHistoryAnalytics(
  platform = false
): Promise<DownloadHistoryAnalytics> {
  const response = await AXIOS_INSTANCE.get<DownloadHistoryAnalytics>(
    '/api/admin/download-history/analytics',
    { params: platform ? { platform: true } : undefined }
  );
  return response.data;
}

export function useDownloadHistoryAnalytics(platform = false) {
  return useQuery({
    queryKey: downloadHistoryAnalyticsQueryKey(platform),
    queryFn: () => fetchDownloadHistoryAnalytics(platform),
    staleTime: 30_000,
  });
}
