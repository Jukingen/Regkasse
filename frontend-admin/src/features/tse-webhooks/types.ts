export interface TseWebhookRegistration {
  id: string;
  tenantId: string;
  url: string;
  events: string[];
  status: string;
  createdAt: string;
  lastDeliveryAt?: string | null;
  lastDeliverySuccess?: boolean | null;
  consecutiveFailures: number;
  hasSecret: boolean;
}

export interface TseWebhookDeliveryResult {
  deliveryId: string;
  webhookId: string;
  eventId: string;
  success: boolean;
  httpStatus?: number | null;
  message?: string | null;
  deliveredAt: string;
}

export interface RegisterTseWebhookRequest {
  tenantId: string;
  url: string;
  events: string[];
  secret?: string;
}

export const TSE_WEBHOOK_EVENT_OPTIONS = [
  'DeviceHealthChanged',
  'CertificateExpiry',
  'FailoverOccurred',
  'Test',
] as const;
