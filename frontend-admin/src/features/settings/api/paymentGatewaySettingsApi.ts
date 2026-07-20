import { customInstance } from '@/lib/axios';

export type PaymentGatewaySettings = {
  provider: string;
  isStripeProvider: boolean;
  apiKeyConfigured: boolean;
  webhookSecretConfigured: boolean;
  requireCardIntentForPosPayments: boolean;
  webhookPath: string;
  onlinePaymentMethods: string[];
};

export type UpdatePaymentGatewaySettingsPayload = {
  onlinePaymentMethods: string[];
};

type ApiDto = {
  provider?: string;
  Provider?: string;
  isStripeProvider?: boolean;
  IsStripeProvider?: boolean;
  apiKeyConfigured?: boolean;
  ApiKeyConfigured?: boolean;
  webhookSecretConfigured?: boolean;
  WebhookSecretConfigured?: boolean;
  requireCardIntentForPosPayments?: boolean;
  RequireCardIntentForPosPayments?: boolean;
  webhookPath?: string;
  WebhookPath?: string;
  onlinePaymentMethods?: string[];
  OnlinePaymentMethods?: string[];
};

const DEFAULT_METHODS = ['card', 'cash', 'online'] as const;

function mapFromApi(dto: ApiDto): PaymentGatewaySettings {
  const methods = dto.onlinePaymentMethods ?? dto.OnlinePaymentMethods ?? [...DEFAULT_METHODS];
  return {
    provider: dto.provider ?? dto.Provider ?? 'Mock',
    isStripeProvider: dto.isStripeProvider ?? dto.IsStripeProvider ?? false,
    apiKeyConfigured: dto.apiKeyConfigured ?? dto.ApiKeyConfigured ?? false,
    webhookSecretConfigured:
      dto.webhookSecretConfigured ?? dto.WebhookSecretConfigured ?? false,
    requireCardIntentForPosPayments:
      dto.requireCardIntentForPosPayments ??
      dto.RequireCardIntentForPosPayments ??
      false,
    webhookPath: dto.webhookPath ?? dto.WebhookPath ?? '/api/webhooks/stripe',
    onlinePaymentMethods: methods.map((m) => m.toLowerCase()),
  };
}

export async function fetchPaymentGatewaySettings(): Promise<PaymentGatewaySettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/admin/settings/payment-gateway',
    method: 'GET',
  });
  return mapFromApi(res);
}

export async function updatePaymentGatewaySettings(
  payload: UpdatePaymentGatewaySettingsPayload,
): Promise<PaymentGatewaySettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/admin/settings/payment-gateway',
    method: 'PUT',
    data: { onlinePaymentMethods: payload.onlinePaymentMethods },
  });
  return mapFromApi(res);
}

export function buildWebhookUrl(webhookPath: string): string {
  const base =
    (typeof process !== 'undefined' && process.env.NEXT_PUBLIC_API_BASE_URL
      ? process.env.NEXT_PUBLIC_API_BASE_URL
      : 'https://api.regkasse.at'
    ).replace(/\/+$/, '');
  const path = webhookPath.startsWith('/') ? webhookPath : `/${webhookPath}`;
  return `${base}${path}`;
}
