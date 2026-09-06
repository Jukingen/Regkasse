import { customInstance } from '@/lib/axios';

import type { BackupHistoryResponseDto } from '@/api/generated/model';

export type BackupRunsListParams = {
  page?: number;
  pageSize?: number;
  tenantId?: string;
  strategy?: number;
  createdBy?: string;
  fromUtc?: string;
  toUtc?: string;
};

export function getBackupRunsListQueryKey(params?: BackupRunsListParams) {
  return ['/api/admin/backup/runs', params ?? {}] as const;
}

export async function getBackupRunsList(
  params?: BackupRunsListParams
): Promise<BackupHistoryResponseDto> {
  return customInstance<BackupHistoryResponseDto>({
    url: '/api/admin/backup/runs',
    method: 'GET',
    params: {
      page: params?.page,
      pageSize: params?.pageSize,
      strategy: params?.strategy,
      createdBy: params?.createdBy,
      fromUtc: params?.fromUtc,
      toUtc: params?.toUtc,
    },
  });
}

export type BackupDownloadHistoryItem = {
  id: string;
  userId: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
  downloadedAt: string;
  fileName: string;
  fileSize?: number | null;
  ipAddress?: string | null;
};

export type BackupDownloadHistoryResponse = {
  runId: string;
  downloadCount: number;
  items: BackupDownloadHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export function getBackupDownloadHistoryQueryKey(runId: string) {
  return ['/api/admin/backup/runs', runId, 'download-history'] as const;
}

export async function getBackupDownloadHistory(
  runId: string
): Promise<BackupDownloadHistoryResponse> {
  return customInstance<BackupDownloadHistoryResponse>({
    url: `/api/admin/backup/runs/${runId}/download-history`,
    method: 'GET',
  });
}
