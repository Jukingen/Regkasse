import { customInstance } from '@/lib/axios';

export const WAL_STATUS_PATH = '/api/admin/backup/pitr/wal-status';

export type WalArchiveHealthLevel = 'Healthy' | 'Degraded' | 'Critical';

export interface WalStatus {
  lastSuccessfulSegmentAtUtc: string | null;
  lastSuccessfulSegmentName: string | null;
  lastAttemptedSegmentAtUtc: string | null;
  lagSeconds: number | null;
  archivedCountLast24h: number;
  failedCountLast24h: number;
  lastFailureReason: string | null;
  healthLevel: WalArchiveHealthLevel;
  healthReason: string;
}

export function getWalStatusQueryKey() {
  return [WAL_STATUS_PATH] as const;
}

export async function getWalStatus(): Promise<WalStatus> {
  return customInstance<WalStatus>({
    url: WAL_STATUS_PATH,
    method: 'GET',
  });
}
