import { customInstance } from '@/lib/axios';

export const DEFAULT_THANK_YOU_MESSAGE = 'Vielen Dank für Ihren Einkauf!';

export type ReceiptSettings = {
  thankYouMessage: string;
  effectiveThankYouMessage: string;
  defaultThankYouMessage: string;
  companyDescription: string;
};

type ApiDto = {
  thankYouMessage?: string | null;
  ThankYouMessage?: string | null;
  effectiveThankYouMessage?: string;
  EffectiveThankYouMessage?: string;
  defaultThankYouMessage?: string;
  DefaultThankYouMessage?: string;
  companyDescription?: string | null;
  CompanyDescription?: string | null;
};

export function mapReceiptSettingsFromApi(dto: ApiDto | null | undefined): ReceiptSettings {
  const defaultMessage =
    dto?.defaultThankYouMessage ?? dto?.DefaultThankYouMessage ?? DEFAULT_THANK_YOU_MESSAGE;
  const stored = dto?.thankYouMessage ?? dto?.ThankYouMessage ?? null;
  const effective =
    dto?.effectiveThankYouMessage ?? dto?.EffectiveThankYouMessage ?? defaultMessage;
  const description = dto?.companyDescription ?? dto?.CompanyDescription ?? '';
  return {
    thankYouMessage: typeof stored === 'string' ? stored : '',
    effectiveThankYouMessage: effective,
    defaultThankYouMessage: defaultMessage,
    companyDescription: typeof description === 'string' ? description : '',
  };
}

export async function fetchReceiptSettings(): Promise<ReceiptSettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/admin/settings/receipt',
    method: 'GET',
  });
  return mapReceiptSettingsFromApi(res);
}

export async function updateReceiptSettings(thankYouMessage: string): Promise<ReceiptSettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/admin/settings/receipt',
    method: 'POST',
    data: { thankYouMessage },
  });
  return mapReceiptSettingsFromApi(res);
}
