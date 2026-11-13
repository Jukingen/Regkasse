import type { FinanzOnlineOutboxItemDto } from '@/api/generated/model/finanzOnlineOutboxItemDto';
import { customInstance } from '@/lib/axios';

export type OutboxStatusGroup = 'Pending' | 'Sent' | 'Failed';

export const OUTBOX_STATUS_QUERY: Record<OutboxStatusGroup, string> = {
  Pending: 'Pending',
  Sent: 'ProtocolSuccess',
  Failed: 'RetryableFailure,PermanentFailure,ProtocolFailure,DeadLetter,ManualReviewRequired',
};

export const OUTBOX_FAILED_STATUSES = new Set([
  'RetryableFailure',
  'PermanentFailure',
  'ProtocolFailure',
  'DeadLetter',
  'ManualReviewRequired',
]);

export type OutboxUiDetail = FinanzOnlineOutboxItemDto & {
  payload?: unknown;
};

export type OutboxListQuery = {
  status?: OutboxStatusGroup;
  fromUtc?: string;
  toUtc?: string;
};

export async function fetchFinanzOnlineOutboxList(
  query: OutboxListQuery,
): Promise<{ items: FinanzOnlineOutboxItemDto[]; total: number }> {
  const params = new URLSearchParams();
  if (query.status) params.set('status', OUTBOX_STATUS_QUERY[query.status]);
  if (query.fromUtc) params.set('fromUtc', query.fromUtc);
  if (query.toUtc) params.set('toUtc', query.toUtc);
  params.set('limit', '500');
  const data = await customInstance<{ items?: FinanzOnlineOutboxItemDto[]; total?: number }>({
    url: `/api/admin/finanzonline-outbox?${params.toString()}`,
    method: 'GET',
  });
  return { items: data.items ?? [], total: data.total ?? data.items?.length ?? 0 };
}

export async function fetchFinanzOnlineOutboxDetail(id: string): Promise<OutboxUiDetail> {
  return customInstance<OutboxUiDetail>({
    url: `/api/admin/finanzonline-outbox/${id}`,
    method: 'GET',
  });
}

export async function retryOutboxMessage(id: string): Promise<void> {
  await customInstance({
    url: `/api/admin/finanzonline-outbox/${id}/retry`,
    method: 'POST',
  });
}

export async function retryOutboxMessages(ids: string[]): Promise<void> {
  for (const id of ids) {
    await retryOutboxMessage(id);
  }
}

export async function retryAllFailedOutbox(body: {
  cashRegisterId?: string;
  fromUtc?: string;
  toUtc?: string;
} = {}): Promise<void> {
  await customInstance({
    url: '/api/admin/finanzonline-outbox/retry-failed',
    method: 'POST',
    data: body,
  });
}

export async function markOutboxResolved(id: string, reason: string): Promise<void> {
  await customInstance({
    url: `/api/admin/finanzonline-outbox/${id}/mark-resolved`,
    method: 'POST',
    data: { reason },
  });
}
