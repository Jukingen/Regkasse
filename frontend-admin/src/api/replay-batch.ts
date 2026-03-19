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
}

export async function getReplayBatchDetail(correlationId: string): Promise<ReplayBatchDetailResponse> {
  return customInstance<ReplayBatchDetailResponse>({
    url: `/api/admin/replay-batch/${encodeURIComponent(correlationId)}`,
    method: 'GET',
  });
}
