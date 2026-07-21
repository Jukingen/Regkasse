'use client';

import type { UseQueryOptions } from '@tanstack/react-query';

import {
  getGetApiAdminBackupListQueryKey,
  useGetApiAdminBackupList,
} from '@/api/generated/admin/admin';
import type { BackupListItemResponseDto } from '@/api/generated/model';
import { BACKUP_DASHBOARD_STATS_POLL_MS } from '@/features/backup/logic/backupDashboardStatsApi';

export type { BackupListItemResponseDto };

export { getGetApiAdminBackupListQueryKey as getBackupListQueryKey };

export type UseBackupListOptions = {
  enabled?: boolean;
  staleTime?: number;
  refetchInterval?: number | false;
};

/** GET /api/admin/backup/list — readable logical dump rows with linked manifest metadata. */
export function useBackupList(options?: UseBackupListOptions) {
  return useGetApiAdminBackupList({
    query: {
      staleTime: options?.staleTime ?? 15_000,
      refetchInterval: options?.refetchInterval ?? BACKUP_DASHBOARD_STATS_POLL_MS,
      refetchOnWindowFocus: true,
      enabled: options?.enabled,
    } satisfies Pick<
      UseQueryOptions<BackupListItemResponseDto[]>,
      'staleTime' | 'refetchInterval' | 'refetchOnWindowFocus' | 'enabled'
    >,
  });
}
