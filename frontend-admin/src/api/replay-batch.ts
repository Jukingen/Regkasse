/** Admin replay batch detail — same fiscal/audit aggregation as in `ReplayBatchDetailAssembler` (backend). */
import { customInstance } from '@/lib/axios';

export interface ReplayBatchPaymentItemDto {
  offlineTransactionId?: string | null;
  paymentId: string;
  receiptId?: string | null;
  receiptNumber?: string | null;
  totalAmount: number;
  createdAtUtc: string;
}

export interface ReplayBatchDetailResponse {
  correlationId: string;
  totalItems: number;
  successCount: number;
  failedOrDuplicateCount: number;
  auditCorrelationId: string;
  payments: ReplayBatchPaymentItemDto[];
  /** Observability replay samples (supplementary). */
  coverageSampleCount?: number;
  /** Audit OFFLINE_SYNCED count for this batch correlation. */
  offlineSyncedAuditCount?: number;
  /** Terminal offline replay failure audit events in this batch. */
  offlineFinalFailureAuditCount?: number;
}

export async function getReplayBatchDetail(correlationId: string): Promise<ReplayBatchDetailResponse> {
  return customInstance<ReplayBatchDetailResponse>({
    url: `/api/admin/replay-batch/${encodeURIComponent(correlationId)}`,
    method: 'GET',
  });
}
