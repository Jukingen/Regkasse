import { customInstance } from '@/lib/axios';

import type {
  RegisterTseWebhookRequest,
  TseWebhookDeliveryResult,
  TseWebhookRegistration,
} from '../types';

export async function listTseWebhooks(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseWebhookRegistration[]> {
  return customInstance<TseWebhookRegistration[]>({
    url: '/api/admin/tse/webhooks',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function createTseWebhook(
  body: RegisterTseWebhookRequest
): Promise<TseWebhookRegistration> {
  return customInstance<TseWebhookRegistration>({
    url: '/api/admin/tse/webhooks',
    method: 'POST',
    data: body,
  });
}

export async function deleteTseWebhook(webhookId: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/tse/webhooks/${webhookId}`,
    method: 'DELETE',
  });
}

export async function testTseWebhook(webhookId: string): Promise<TseWebhookDeliveryResult> {
  return customInstance<TseWebhookDeliveryResult>({
    url: `/api/admin/tse/webhooks/${webhookId}/test`,
    method: 'POST',
  });
}
