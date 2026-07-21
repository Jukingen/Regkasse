import { apiClient, TokenManager } from './config'; // ✅ YENİ: TokenManager import
import {
  readCashRegisterIdFromSettingsPayload,
  resolveUserSettingsRecord,
} from './normalizeUserSettingsResponse';
import { debugPosPaymentTrace } from '../../utils/debugPosPaymentTrace';

const isDev = __DEV__;

// Kullanıcı ayarları interface'i - Kasa mantığına uygun
export interface UserSettings {
  id: string;
  userId: string;

  // Dil ve lokalizasyon ayarları
  language: 'de-DE' | 'en' | 'tr';
  currency: 'EUR' | 'USD' | 'TRY';
  dateFormat: 'DD.MM.YYYY' | 'MM/DD/YYYY' | 'YYYY-MM-DD';
  timeFormat: '24h' | '12h';

  // Kasa konfigürasyonu
  cashRegisterId?: string;
  defaultTaxRate: number; // Varsayılan vergi oranı (%)
  enableDiscounts: boolean; // İndirim sistemi aktif mi?
  enableCoupons: boolean; // Kupon sistemi aktif mi?
  autoPrintReceipts: boolean; // Otomatik fiş yazdırma
  receiptHeader?: string; // Fiş başlığı
  receiptFooter?: string; // Fiş alt bilgisi

  // TSE ve FinanzOnline ayarları
  tseDeviceId?: string;
  finanzOnlineEnabled: boolean;
  finanzOnlineUsername?: string;

  // Güvenlik ayarları
  sessionTimeout: number; // Dakika cinsinden oturum süresi
  requirePinForRefunds: boolean; // İade için PIN gerekli mi?
  maxDiscountPercentage: number; // Maksimum indirim yüzdesi

  // Görünüm ayarları
  theme: 'light' | 'dark' | 'auto';
  compactMode: boolean; // Kompakt görünüm
  showProductImages: boolean; // Ürün resimleri gösterilsin mi?

  // Bildirim ayarları
  enableNotifications: boolean;
  lowStockAlert: boolean; // Düşük stok uyarısı
  dailyReportEmail?: string; // Günlük rapor email'i

  // Varsayılan değerler
  defaultPaymentMethod: 'cash' | 'card' | 'mixed' | 'voucher' | 'transfer';
  defaultTableNumber?: string;
  defaultWaiterName?: string;

  createdAt: string;
  updatedAt: string;
}

// Kullanıcı ayarlarını getir
export const getUserSettings = async (): Promise<UserSettings> => {
  try {
    if (isDev) {
      console.log('Fetching user settings from API...');
    }
    // ✅ YENİ: apiClient otomatik header management
    const raw = await apiClient.get<unknown>('/user/settings');
    if (isDev) {
      console.log('User settings API response (dev): keys loaded');
    }
    const source = resolveUserSettingsRecord(raw);
    const flat = source;
    debugPosPaymentTrace('settings_values', {
      cashRegisterId: flat.cashRegisterId ?? flat.CashRegisterId ?? null,
      userId: flat.userId ?? flat.UserId ?? null,
      userNavNull: flat.user === null || flat.User === null,
      topKeys: typeof flat === 'object' && flat ? Object.keys(flat).slice(0, 25) : [],
    });
    const id = readCashRegisterIdFromSettingsPayload(source);
    const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
    return { ...(source as unknown as UserSettings), cashRegisterId: invalid ? undefined : id };
  } catch (error) {
    if (isDev) {
      console.error('Error fetching user settings:', error);
    }
    debugPosPaymentTrace('settings_fetch_failed_throw', {
      message: error instanceof Error ? error.message : String(error),
    });
    throw error instanceof Error ? error : new Error('User settings could not be loaded');
  }
};

/**
 * POST /user/settings/bootstrap — creates UserSettings row if missing and applies sole-register auto-assignment when eligible.
 * Does not replace GET for routine reads; use after login (see getUserSettingsAfterLogin) or when explicit initialization is required.
 */
export const bootstrapUserSettings = async (): Promise<UserSettings> => {
  const raw = await apiClient.post<unknown>('/user/settings/bootstrap', {});
  const source = resolveUserSettingsRecord(raw);
  const flat = source;
  debugPosPaymentTrace('settings_bootstrap_values', {
    cashRegisterId: flat.cashRegisterId ?? flat.CashRegisterId ?? null,
    userId: flat.userId ?? flat.UserId ?? null,
  });
  const id = readCashRegisterIdFromSettingsPayload(source);
  const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
  return { ...(source as unknown as UserSettings), cashRegisterId: invalid ? undefined : id };
};

/**
 * Preferred path after login: bootstrap (explicit mutation + sole assign), then GET if bootstrap is unavailable (older API).
 */
export const getUserSettingsAfterLogin = async (): Promise<UserSettings> => {
  try {
    return await bootstrapUserSettings();
  } catch (error) {
    if (isDev) {
      console.warn('[userSettings] bootstrap failed, falling back to GET /user/settings', error);
    }
    return await getUserSettings();
  }
};

// Kullanıcı ayarlarını güncelle
export const updateUserSettings = async (
  settings: Partial<UserSettings>
): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings', settings);
    return response;
  } catch (error) {
    if (isDev) {
      console.error('Error updating user settings:', error);
    }
    throw new Error('Kullanıcı ayarları güncellenemedi');
  }
};

// Kullanıcı dilini güncelle
export const updateUserLanguage = async (
  language: 'de-DE' | 'en' | 'tr'
): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings/language', { language });
    return response;
  } catch (error) {
    if (isDev) {
      console.error('Error updating user language:', error);
    }
    throw new Error('Dil ayarı güncellenemedi');
  }
};

// Kasa konfigürasyonunu güncelle
export const updateCashRegisterConfig = async (config: {
  cashRegisterId?: string;
  defaultTaxRate?: number;
  enableDiscounts?: boolean;
  enableCoupons?: boolean;
  autoPrintReceipts?: boolean;
  receiptHeader?: string;
  receiptFooter?: string;
}): Promise<UserSettings> => {
  try {
    const raw = await apiClient.put<unknown>('/user/settings/cash-register', config);
    const source = resolveUserSettingsRecord(raw);
    const id = readCashRegisterIdFromSettingsPayload(source);
    const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
    return { ...(source as unknown as UserSettings), cashRegisterId: invalid ? undefined : id };
  } catch (error) {
    if (isDev) {
      console.error('Error updating cash register config:', error);
    }
    // Preserve { status, data } from axios interceptor so callers can distinguish policy 4xx vs transient failures.
    if (error && typeof error === 'object' && 'status' in error) {
      throw error;
    }
    throw error instanceof Error ? error : new Error('Kasa konfigürasyonu güncellenemedi');
  }
};

// TSE ayarlarını güncelle
export const updateTSESettings = async (settings: {
  tseDeviceId?: string;
  finanzOnlineEnabled?: boolean;
  finanzOnlineUsername?: string;
}): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings/tse', settings);
    return response;
  } catch (error) {
    if (isDev) {
      console.error('Error updating TSE settings:', error);
    }
    throw new Error('TSE ayarları güncellenemedi');
  }
};

// Güvenlik ayarlarını güncelle
export const updateSecuritySettings = async (settings: {
  sessionTimeout?: number;
  requirePinForRefunds?: boolean;
  maxDiscountPercentage?: number;
}): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings/security', settings);
    return response;
  } catch (error) {
    if (isDev) {
      console.error('Error updating security settings:', error);
    }
    throw new Error('Güvenlik ayarları güncellenemedi');
  }
};

// Varsayılan kullanıcı ayarları
const getDefaultUserSettings = (): UserSettings => {
  return {
    id: 'default',
    userId: 'default',

    // Varsayılan dil ve lokalizasyon (Avusturya için)
    language: 'de-DE',
    currency: 'EUR',
    dateFormat: 'DD.MM.YYYY',
    timeFormat: '24h',

    // Varsayılan kasa ayarları
    defaultTaxRate: 20, // Avusturya standart vergi oranı
    enableDiscounts: true,
    enableCoupons: true,
    autoPrintReceipts: false,
    receiptHeader: 'Registrierkasse - Kassenbeleg',
    receiptFooter: 'Vielen Dank für Ihren Einkauf!',

    // TSE ayarları
    finanzOnlineEnabled: false,

    // Güvenlik ayarları
    sessionTimeout: 30, // 30 dakika
    requirePinForRefunds: true,
    maxDiscountPercentage: 50,

    // Görünüm ayarları
    theme: 'light',
    compactMode: false,
    showProductImages: true,

    // Bildirim ayarları
    enableNotifications: true,
    lowStockAlert: true,

    // Varsayılan değerler
    defaultPaymentMethod: 'mixed',
    defaultTableNumber: '1',
    defaultWaiterName: 'Kasiyer',

    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
};

// Kullanıcı ayarlarını sıfırla (varsayılan değerlere döndür)
export const resetUserSettings = async (): Promise<UserSettings> => {
  try {
    const response = await apiClient.post<UserSettings>('/user/settings/reset');
    return response;
  } catch (error) {
    if (isDev) {
      console.error('Error resetting user settings:', error);
    }
    // Varsayılan ayarları döndür
    return getDefaultUserSettings();
  }
};

// Kullanıcı ayarlarını export et
export { getDefaultUserSettings };
