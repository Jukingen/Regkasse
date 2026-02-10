import { apiClient, TokenManager } from './config'; // ✅ YENİ: TokenManager import
import AsyncStorage from '@react-native-async-storage/async-storage';

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
  defaultPaymentMethod: 'cash' | 'card' | 'mixed';
  defaultTableNumber?: string;
  defaultWaiterName?: string;
  
  createdAt: string;
  updatedAt: string;
}

// Kullanıcı ayarlarını getir
export const getUserSettings = async (): Promise<UserSettings> => {
  try {
    console.log('Fetching user settings from API...');
    
    // ✅ YENİ: TokenManager ile token kontrolü - manual header ekleme gerekmez
    // apiClient zaten otomatik token management yapıyor
    console.log('User settings request - token management via apiClient');
    
    // ✅ YENİ: apiClient otomatik header management
    const response = await apiClient.get<UserSettings>('/user/settings');
    console.log('User settings API response:', response);
    return response;
  } catch (error) {
    console.error('Error fetching user settings:', error);
    // Varsayılan ayarları döndür
    return getDefaultUserSettings();
  }
};

// Kullanıcı ayarlarını güncelle
export const updateUserSettings = async (settings: Partial<UserSettings>): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings', settings);
    return response;
  } catch (error) {
    console.error('Error updating user settings:', error);
    throw new Error('Kullanıcı ayarları güncellenemedi');
  }
};

// Kullanıcı dilini güncelle
export const updateUserLanguage = async (language: 'de-DE' | 'en' | 'tr'): Promise<UserSettings> => {
  try {
    const response = await apiClient.put<UserSettings>('/user/settings/language', { language });
    return response;
  } catch (error) {
    console.error('Error updating user language:', error);
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
    const response = await apiClient.put<UserSettings>('/user/settings/cash-register', config);
    return response;
  } catch (error) {
    console.error('Error updating cash register config:', error);
    throw new Error('Kasa konfigürasyonu güncellenemedi');
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
    console.error('Error updating TSE settings:', error);
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
    console.error('Error updating security settings:', error);
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
    console.error('Error resetting user settings:', error);
    // Varsayılan ayarları döndür
    return getDefaultUserSettings();
  }
};

// Kullanıcı ayarlarını export et
export { getDefaultUserSettings };
