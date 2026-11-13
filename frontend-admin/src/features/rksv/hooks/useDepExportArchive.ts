'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';
import { depExportHistoryQueryKey } from '@/features/rksv/hooks/useDepExportHistory';

export type DepExportArchiveSummaryItemDto = {
  exportId: string;
  cashRegisterId: string;
  fileName: string;
  exportedAt: string;
  fileSizeBytes: number;
  archivedAt?: string | null;
  retentionUntil?: string | null;
  purgedAt?: string | null;
  archiveChecksum?: string | null;
  hasArchiveFile: boolean;
};

export type DepExportArchiveReportDto = {
  tenantId: string;
  generatedAtUtc: string;
  totalCompletedExports: number;
  archivedCount: number;
  pendingArchiveCount: number;
  purgedCount: number;
  retentionYears: number;
  totalArchivedSizeBytes: number;
  oldestArchivedExportAt?: string | null;
  recent: DepExportArchiveSummaryItemDto[];
};

export type DepExportArchiveResultDto = {
  exportId: string;
  success: boolean;
  archivePath?: string | null;
  checksum?: string | null;
  retentionUntil?: string | null;
  archivedAt?: string | null;
  errorMessage?: string | null;
  alreadyArchived?: boolean;
};

export const depExportArchiveQueryKeys = {
  all: ['rksv', 'dep-export-archive'] as const,
  report: ['rksv', 'dep-export-archive', 'report'] as const,
};

export async function fetchDepExportArchiveReport(): Promise<DepExportArchiveReportDto> {
  const response = await AXIOS_INSTANCE.get<DepExportArchiveReportDto>(
    '/api/admin/rksv/dep-export/archive-report'
  );
  return {
    ...response.data,
    recent: response.data.recent ?? [],
    totalArchivedSizeBytes: response.data.totalArchivedSizeBytes ?? 0,
    archivedCount: response.data.archivedCount ?? 0,
    pendingArchiveCount: response.data.pendingArchiveCount ?? 0,
    purgedCount: response.data.purgedCount ?? 0,
  };
}

export async function runDepExportArchive(exportId: string): Promise<DepExportArchiveResultDto> {
  const response = await AXIOS_INSTANCE.post<DepExportArchiveResultDto>(
    `/api/admin/rksv/dep-export/history/${exportId}/archive`
  );
  return response.data;
}

export function useDepExportArchiveReport() {
  return useQuery({
    queryKey: depExportArchiveQueryKeys.report,
    queryFn: fetchDepExportArchiveReport,
    staleTime: 30_000,
  });
}

export function useRunDepExportArchive() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (exportId: string) => runDepExportArchive(exportId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: depExportArchiveQueryKeys.all }),
        queryClient.invalidateQueries({ queryKey: depExportHistoryQueryKey() }),
      ]);
    },
  });
}

/** Active (non-purged) archived rows for the UI table. */
export function selectActiveArchivedExports(
  report: DepExportArchiveReportDto | undefined
): DepExportArchiveSummaryItemDto[] {
  return (report?.recent ?? []).filter((row) => Boolean(row.archivedAt) && !row.purgedAt);
}

export function bytesToMegabytes(bytes: number): number {
  if (!Number.isFinite(bytes) || bytes <= 0) return 0;
  return Math.round((bytes / (1024 * 1024)) * 100) / 100;
}
