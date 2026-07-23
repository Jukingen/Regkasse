import { customInstance } from '@/lib/axios';

export type OperationLimitStatus = {
  enabled: boolean;
  maxBulkDeletePerDay: number;
  maxPriceUpdatePerHour: number;
  maxProductCreatePerDay: number;
  maxUserCreatePerDay: number;
  maxBackupPerDay: number;
  maxExportPerDay: number;
  requireApprovalForBulkDelete: number;
  requireApprovalForPriceUpdate: number;
  bulkDeleteUsedToday: number;
  bulkDeleteRemainingToday: number;
  bulkDeleteResetAtUtc: string;
  priceUpdateUsedThisHour: number;
  priceUpdateRemainingThisHour: number;
  priceUpdateResetAtUtc: string;
};

export async function getOperationLimitStatus(): Promise<OperationLimitStatus> {
  return customInstance<OperationLimitStatus>({
    url: '/api/admin/operation-limits/status',
    method: 'GET',
  });
}
