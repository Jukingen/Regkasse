import { customInstance } from '@/lib/axios';

export type OfflineSettings = {
  maxOfflineTransactions: number;
  maxOfflineOrders: number;
  offlineExpiryHours: number;
  tokenExpiryHours: number;
  enableOfflineOrders: boolean;
  enableOfflinePayments: boolean;
};

export type UpdateOfflineSettingsPayload = OfflineSettings;

type OfflineSettingsApiDto = {
  maxOfflineTransactions?: number;
  maxOfflineOrders?: number;
  offlineExpiryHours?: number;
  tokenExpiryHours?: number;
  enableOfflineOrders?: boolean;
  enableOfflinePayments?: boolean;
  MaxOfflineTransactions?: number;
  MaxOfflineOrders?: number;
  OfflineExpiryHours?: number;
  TokenExpiryHours?: number;
  EnableOfflineOrders?: boolean;
  EnableOfflinePayments?: boolean;
};

const DEFAULT_OFFLINE_SETTINGS: OfflineSettings = {
  maxOfflineTransactions: 50,
  maxOfflineOrders: 100,
  offlineExpiryHours: 72,
  tokenExpiryHours: 168,
  enableOfflineOrders: true,
  enableOfflinePayments: true,
};

function mapFromApi(dto: OfflineSettingsApiDto): OfflineSettings {
  return {
    maxOfflineTransactions:
      dto.maxOfflineTransactions ??
      dto.MaxOfflineTransactions ??
      DEFAULT_OFFLINE_SETTINGS.maxOfflineTransactions,
    maxOfflineOrders:
      dto.maxOfflineOrders ?? dto.MaxOfflineOrders ?? DEFAULT_OFFLINE_SETTINGS.maxOfflineOrders,
    offlineExpiryHours:
      dto.offlineExpiryHours ??
      dto.OfflineExpiryHours ??
      DEFAULT_OFFLINE_SETTINGS.offlineExpiryHours,
    tokenExpiryHours:
      dto.tokenExpiryHours ?? dto.TokenExpiryHours ?? DEFAULT_OFFLINE_SETTINGS.tokenExpiryHours,
    enableOfflineOrders:
      dto.enableOfflineOrders ??
      dto.EnableOfflineOrders ??
      DEFAULT_OFFLINE_SETTINGS.enableOfflineOrders,
    enableOfflinePayments:
      dto.enableOfflinePayments ??
      dto.EnableOfflinePayments ??
      DEFAULT_OFFLINE_SETTINGS.enableOfflinePayments,
  };
}

export async function fetchOfflineSettings(): Promise<OfflineSettings> {
  const res = await customInstance<OfflineSettingsApiDto>({
    url: '/api/admin/settings/offline',
    method: 'GET',
  });
  return mapFromApi(res);
}

export async function updateOfflineSettings(
  payload: UpdateOfflineSettingsPayload
): Promise<OfflineSettings> {
  const res = await customInstance<OfflineSettingsApiDto>({
    url: '/api/admin/settings/offline',
    method: 'PUT',
    data: payload,
  });
  return mapFromApi(res);
}

export { DEFAULT_OFFLINE_SETTINGS };
