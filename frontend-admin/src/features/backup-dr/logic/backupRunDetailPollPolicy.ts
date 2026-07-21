/**
 * Run-by-id sorgusu için refetch aralığı: terminal geçişte detail API bazen geride kalır — eşleşene kadar kısa aralık, sonra dur.
 */
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupRunResponseDtoStatus } from '@/api/generated/model';

/** Latest-status ile aynı “aktif” tanımı — kuyruk / çalışıyor / doğrulama bekliyor. */
export function isBackupLatestRunActiveStatus(status: number | undefined): boolean {
  return (
    status === BackupRunResponseDtoStatus.NUMBER_0 ||
    status === BackupRunResponseDtoStatus.NUMBER_1 ||
    status === BackupRunResponseDtoStatus.NUMBER_2
  );
}

export const BACKUP_ACTIVE_POLL_MS = 8_000;
export const RUN_DETAIL_CATCH_UP_POLL_MS = 5_000;

/**
 * React Query `refetchInterval` için: `false` = yenileme yok; sayı = ms.
 * Stale detail (status uyumsuz veya henüz yüklenmedi) için catch-up aralığı döner.
 */
export function computeRunDetailRefetchIntervalMs(params: {
  latestRunId: string | undefined;
  latestStatus: number | undefined;
  detail: BackupRunResponseDto | undefined;
}): number | false {
  if (!params.latestRunId) return false;
  const { latestStatus, detail } = params;
  if (isBackupLatestRunActiveStatus(latestStatus)) return BACKUP_ACTIVE_POLL_MS;
  if (detail === undefined) return RUN_DETAIL_CATCH_UP_POLL_MS;
  if (detail.status !== latestStatus) return RUN_DETAIL_CATCH_UP_POLL_MS;
  return false;
}
