'use client';

import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DepExportStatisticsDto = {
  totalExports: number;
  successfulExports: number;
  failedExports: number;
  successRate: number;
  exportsByType: Record<string, number>;
  exportsByYear: Record<string, number>;
  averageExportSizeBytes: number;
  totalStorageUsedMb: number;
  lastExportDate?: string | null;
  nextDueDate?: string | null;
  fromUtc: string;
  toUtc: string;
};

export type DepExportTrendPointDto = {
  periodStartUtc: string;
  label: string;
  totalExports: number;
  successfulExports: number;
  failedExports: number;
  totalSizeBytes: number;
};

export type DepExportForecastPointDto = {
  periodStartUtc: string;
  label: string;
  projectedExports: number;
  hasKnownDueDate: boolean;
};

export type DepExportForecastDto = {
  generatedAtUtc: string;
  nextDueDate?: string | null;
  nextRequirementTitle?: string | null;
  averageMonthlyExports: number;
  points: DepExportForecastPointDto[];
  method?: string;
};

export const depExportStatisticsQueryKeys = {
  all: ['rksv', 'dep-export-statistics'] as const,
  stats: (fromUtc?: string, toUtc?: string) =>
    ['rksv', 'dep-export-statistics', 'summary', fromUtc ?? 'default', toUtc ?? 'default'] as const,
  trend: (months: number) => ['rksv', 'dep-export-statistics', 'trend', months] as const,
  forecast: ['rksv', 'dep-export-statistics', 'forecast'] as const,
};

export async function fetchDepExportStatistics(
  fromUtc?: string,
  toUtc?: string
): Promise<DepExportStatisticsDto> {
  const response = await AXIOS_INSTANCE.get<DepExportStatisticsDto>(
    '/api/admin/rksv/dep-export/statistics',
    { params: { fromUtc, toUtc } }
  );
  return {
    ...response.data,
    exportsByType: response.data.exportsByType ?? {},
    exportsByYear: response.data.exportsByYear ?? {},
  };
}

export async function fetchDepExportTrend(months = 12): Promise<DepExportTrendPointDto[]> {
  const response = await AXIOS_INSTANCE.get<DepExportTrendPointDto[]>(
    '/api/admin/rksv/dep-export/statistics/trend',
    { params: { months } }
  );
  return response.data ?? [];
}

export async function fetchDepExportForecast(): Promise<DepExportForecastDto> {
  const response = await AXIOS_INSTANCE.get<DepExportForecastDto>(
    '/api/admin/rksv/dep-export/statistics/forecast'
  );
  return {
    ...response.data,
    points: response.data.points ?? [],
  };
}

export function useDepExportStatistics(fromUtc?: string, toUtc?: string) {
  return useQuery({
    queryKey: depExportStatisticsQueryKeys.stats(fromUtc, toUtc),
    queryFn: () => fetchDepExportStatistics(fromUtc, toUtc),
    staleTime: 30_000,
  });
}

export function useDepExportTrend(months = 12) {
  return useQuery({
    queryKey: depExportStatisticsQueryKeys.trend(months),
    queryFn: () => fetchDepExportTrend(months),
    staleTime: 30_000,
  });
}

export function useDepExportForecast() {
  return useQuery({
    queryKey: depExportStatisticsQueryKeys.forecast,
    queryFn: fetchDepExportForecast,
    staleTime: 60_000,
  });
}

export function averageExportSizeMb(bytes: number): number {
  if (!bytes || bytes <= 0) return 0;
  return Math.round((bytes / (1024 * 1024)) * 100) / 100;
}
