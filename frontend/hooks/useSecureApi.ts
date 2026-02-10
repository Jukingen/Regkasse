import { useEffect } from 'react';
import { usePermission } from './usePermission';
import { secureApi } from '../services/api/secureApiService';
import { UserRole } from '../types/auth';

/**
 * Güvenli API kullanımı için hook - JWT ve role-based erişim kontrolü
 * Türkçe açıklama: Bu hook SecureApiService ile usePermission'ı entegre eder
 */
export const useSecureApi = () => {
  const permissionHook = usePermission();

  // Permission hook'unu SecureApiService'e bağla
  useEffect(() => {
    secureApi.setPermissionHook(permissionHook);
  }, [permissionHook]);

  /**
   * Güvenli GET isteği
   * Türkçe açıklama: Yetki kontrolü ile GET isteği
   */
  const secureGet = async <T>(
    url: string,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> => {
    return await secureApi.secureGet<T>(url, requiredPermission, requiredRole, config);
  };

  /**
   * Güvenli POST isteği
   * Türkçe açıklama: Yetki kontrolü ile POST isteği
   */
  const securePost = async <T>(
    url: string,
    data?: any,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> => {
    return await secureApi.securePost<T>(url, data, requiredPermission, requiredRole, config);
  };

  /**
   * Güvenli PUT isteği
   * Türkçe açıklama: Yetki kontrolü ile PUT isteği
   */
  const securePut = async <T>(
    url: string,
    data?: any,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> => {
    return await secureApi.securePut<T>(url, data, requiredPermission, requiredRole, config);
  };

  /**
   * Güvenli DELETE isteği
   * Türkçe açıklama: Yetki kontrolü ile DELETE isteği
   */
  const secureDelete = async <T>(
    url: string,
    requiredPermission?: { resource: string; action: string },
    requiredRole?: UserRole,
    config?: any
  ): Promise<T> => {
    return await secureApi.secureDelete<T>(url, requiredPermission, requiredRole, config);
  };

  /**
   * Kritik işlemler için güvenli operasyon
   * Türkçe açıklama: Kritik işlemler için ek güvenlik
   */
  const criticalOperation = async <T>(
    operation: () => Promise<T>,
    operationName: string
  ): Promise<T> => {
    return await secureApi.criticalOperation(operation, operationName);
  };

  /**
   * Demo kullanıcı kısıtlaması kontrolü
   * Türkçe açıklama: Demo kullanıcılar için kısıtlama
   */
  const checkDemoRestriction = (operationName: string): boolean => {
    return secureApi.checkDemoUserRestriction(operationName);
  };

  /**
   * Token durumu kontrolü
   * Türkçe açıklama: Token geçerlilik kontrolü
   */
  const checkTokenStatus = async (): Promise<boolean> => {
    return await secureApi.checkTokenStatus();
  };

  return {
    // Güvenli API metodları
    secureGet,
    securePost,
    securePut,
    secureDelete,
    criticalOperation,
    
    // Güvenlik kontrolleri
    checkDemoRestriction,
    checkTokenStatus,
    
    // Permission hook'undan gelen özellikler
    ...permissionHook
  };
}; 