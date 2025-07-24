import { Alert } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient, TokenManager } from './config';
import { usePermission } from '../../hooks/usePermission';
import { UserRole } from '../../types/auth';

/**
 * Güvenli API servisi - JWT token kontrolü ve role-based erişim kontrolü
 * Türkçe açıklama: Bu servis tüm API çağrılarında güvenlik kontrolü yapar
 */
export class SecureApiService {
  private static instance: SecureApiService;
  private permissionHook: ReturnType<typeof usePermission> | null = null;

  private constructor() {}

  public static getInstance(): SecureApiService {
    if (!SecureApiService.instance) {
      SecureApiService.instance = new SecureApiService();
    }
    return SecureApiService.instance;
  }

  /**
   * Permission hook'unu set et
   * Türkçe açıklama: Hook'u servis ile bağla
   */
  public setPermissionHook(hook: ReturnType<typeof usePermission>) {
    this.permissionHook = hook;
  }

  /**
   * Güvenli GET isteği
   * Türkçe açıklama: Yetki kontrolü ile GET isteği
   */
  public async secureGet<T>(
    url: string, 
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> {
    // Yetki kontrolü
    if (requiredPermission && this.permissionHook) {
      if (!this.permissionHook.checkPermissionWithAlert(requiredPermission.resource, requiredPermission.action)) {
        throw new Error('Insufficient permissions');
      }
    }

    if (requiredRole && this.permissionHook) {
      if (!this.permissionHook.checkRoleWithAlert(requiredRole)) {
        throw new Error('Insufficient role');
      }
    }

    try {
      return await apiClient.get<T>(url, config);
    } catch (error: any) {
      this.handleApiError(error);
      throw error;
    }
  }

  /**
   * Güvenli POST isteği
   * Türkçe açıklama: Yetki kontrolü ile POST isteği
   */
  public async securePost<T>(
    url: string,
    data?: any,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> {
    // Yetki kontrolü
    if (requiredPermission && this.permissionHook) {
      if (!this.permissionHook.checkPermissionWithAlert(requiredPermission.resource, requiredPermission.action)) {
        throw new Error('Insufficient permissions');
      }
    }

    if (requiredRole && this.permissionHook) {
      if (!this.permissionHook.checkRoleWithAlert(requiredRole)) {
        throw new Error('Insufficient role');
      }
    }

    try {
      return await apiClient.post<T>(url, data, config);
    } catch (error: any) {
      this.handleApiError(error);
      throw error;
    }
  }

  /**
   * Güvenli PUT isteği
   * Türkçe açıklama: Yetki kontrolü ile PUT isteği
   */
  public async securePut<T>(
    url: string,
    data?: any,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> {
    // Yetki kontrolü
    if (requiredPermission && this.permissionHook) {
      if (!this.permissionHook.checkPermissionWithAlert(requiredPermission.resource, requiredPermission.action)) {
        throw new Error('Insufficient permissions');
      }
    }

    if (requiredRole && this.permissionHook) {
      if (!this.permissionHook.checkRoleWithAlert(requiredRole)) {
        throw new Error('Insufficient role');
      }
    }

    try {
      return await apiClient.put<T>(url, data, config);
    } catch (error: any) {
      this.handleApiError(error);
      throw error;
    }
  }

  /**
   * Güvenli DELETE isteği
   * Türkçe açıklama: Yetki kontrolü ile DELETE isteği
   */
  public async secureDelete<T>(
    url: string,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> {
    // Yetki kontrolü
    if (requiredPermission && this.permissionHook) {
      if (!this.permissionHook.checkPermissionWithAlert(requiredPermission.resource, requiredPermission.action)) {
        throw new Error('Insufficient permissions');
      }
    }

    if (requiredRole && this.permissionHook) {
      if (!this.permissionHook.checkRoleWithAlert(requiredRole)) {
        throw new Error('Insufficient role');
      }
    }

    try {
      return await apiClient.delete<T>(url, config);
    } catch (error: any) {
      this.handleApiError(error);
      throw error;
    }
  }

  /**
   * Kritik işlemler için özel güvenlik kontrolü
   * Türkçe açıklama: Kritik işlemler için ek güvenlik
   */
  public async criticalOperation<T>(
    operation: () => Promise<T>,
    operationName: string
  ): Promise<T> {
    if (this.permissionHook && !this.permissionHook.canPerformCriticalOperations) {
      Alert.alert(
        'Kritik İşlem Hatası',
        `${operationName} işlemi için yeterli yetkiniz bulunmamaktadır.`,
        [{ text: 'Tamam' }]
      );
      throw new Error('Critical operation not allowed');
    }

    try {
      return await operation();
    } catch (error: any) {
      this.handleApiError(error);
      throw error;
    }
  }

  /**
   * Demo kullanıcı kontrolü
   * Türkçe açıklama: Demo kullanıcılar için kısıtlama
   */
  public checkDemoUserRestriction(operationName: string): boolean {
    if (this.permissionHook?.isDemoUser) {
      Alert.alert(
        'Demo Kısıtlaması',
        `Demo kullanıcılar ${operationName} işlemini gerçekleştiremez.`,
        [{ text: 'Tamam' }]
      );
      return false;
    }
    return true;
  }

  /**
   * API hata yönetimi
   * Türkçe açıklama: Merkezi hata yönetimi
   */
  private handleApiError(error: any) {
    console.error('API Error:', error);

    // Token ile ilgili hatalar
    if (error.status === 401) {
      Alert.alert(
        'Oturum Hatası',
        'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.',
        [{ text: 'Tamam' }]
      );
      // Login sayfasına yönlendirme gerekebilir
      return;
    }

    // Yetkisiz erişim
    if (error.status === 403) {
      Alert.alert(
        'Yetkisiz Erişim',
        'Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır.',
        [{ text: 'Tamam' }]
      );
      return;
    }

    // Sunucu hatası
    if (error.status >= 500) {
      Alert.alert(
        'Sunucu Hatası',
        'Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.',
        [{ text: 'Tamam' }]
      );
      return;
    }

    // Genel hata
    if (error.message) {
      Alert.alert(
        'Hata',
        error.message,
        [{ text: 'Tamam' }]
      );
    }
  }

  /**
   * Token durumunu kontrol et
   * Türkçe açıklama: Token geçerlilik kontrolü
   */
  public async checkTokenStatus(): Promise<boolean> {
    try {
      const token = await TokenManager.getTokenInfo(await this.getStoredToken());
      return token && !TokenManager.isTokenExpired(await this.getStoredToken());
    } catch (error) {
      return false;
    }
  }

  /**
   * Saklanan token'ı al
   * Türkçe açıklama: AsyncStorage'dan token alma
   */
  private async getStoredToken(): Promise<string> {
    return await AsyncStorage.getItem('token') || '';
  }
}

// Singleton instance
export const secureApi = SecureApiService.getInstance(); 