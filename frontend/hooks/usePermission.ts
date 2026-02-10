import { useContext, useMemo } from 'react';
import { Alert } from 'react-native';
import { useTranslation } from 'react-i18next';

import { AuthContext } from '../contexts/AuthContext';
import { UsePermissionReturn, UserRole } from '../types/auth';
import { PERMISSIONS } from '../shared/utils/PermissionHelper';

/**
 * Gelişmiş yetki kontrol hook'u - Rol ve kaynak bazlı erişim kontrolü sağlar
 * Türkçe açıklama: Bu hook kullanıcının yetkilerini kontrol eder ve güvenli erişim sağlar
 */
export const usePermission = (): UsePermissionReturn => {
  const { user } = useContext(AuthContext);
  const { t } = useTranslation();

  // Kullanıcının tüm yetkilerini memoize et
  const userPermissions = useMemo(() => {
    if (!user?.permissions) return new Set<string>();
    return new Set(user.permissions);
  }, [user?.permissions]);

  // Kullanıcının rollerini memoize et
  const userRoles = useMemo(() => {
    if (!user?.role) return new Set<string>();
    return new Set([user.role]);
  }, [user?.role]);

  /**
   * Kullanıcının belirli bir kaynak üzerinde belirli bir işlem yapma yetkisi var mı kontrol eder
   * Türkçe açıklama: Granular yetki kontrolü - resource.action formatında kontrol
   */
  const hasPermission = (resource: string, action: string): boolean => {
    if (!user || !userPermissions.size) return false;
    
    const requiredPermission = `${resource}.${action}`;
    return userPermissions.has(requiredPermission);
  };

  /**
   * Kullanıcının belirli bir role sahip olup olmadığını kontrol eder
   * Türkçe açıklama: Rol bazlı erişim kontrolü
   */
  const hasRole = (role: UserRole): boolean => {
    if (!user || !userRoles.size) return false;
    return userRoles.has(role);
  };

  /**
   * Kullanıcının birden fazla rolden herhangi birine sahip olup olmadığını kontrol eder
   * Türkçe açıklama: Çoklu rol kontrolü
   */
  const hasAnyRole = (roles: UserRole[]): boolean => {
    if (!user || !userRoles.size) return false;
    return roles.some(role => userRoles.has(role));
  };

  /**
   * Kullanıcının tüm belirtilen rollere sahip olup olmadığını kontrol eder
   * Türkçe açıklama: Tüm rollerin kontrolü
   */
  const hasAllRoles = (roles: UserRole[]): boolean => {
    if (!user || !userRoles.size) return false;
    return roles.every(role => userRoles.has(role));
  };

  /**
   * Yetki kontrolü yapar ve yetkisiz erişimde uyarı gösterir
   * Türkçe açıklama: Güvenli erişim kontrolü ile kullanıcı uyarısı
   */
  const checkPermissionWithAlert = (resource: string, action: string): boolean => {
    const hasAccess = hasPermission(resource, action);
    
    if (!hasAccess) {
      Alert.alert(
        t('errors.unauthorized', 'Yetkisiz Erişim'),
        t('errors.noPermission', 'Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır.'),
        [{ text: t('common.ok', 'Tamam') }]
      );
    }
    
    return hasAccess;
  };

  /**
   * Rol kontrolü yapar ve yetkisiz erişimde uyarı gösterir
   * Türkçe açıklama: Rol bazlı güvenli erişim kontrolü
   */
  const checkRoleWithAlert = (role: UserRole): boolean => {
    const hasAccess = hasRole(role);
    
    if (!hasAccess) {
      Alert.alert(
        t('errors.unauthorized', 'Yetkisiz Erişim'),
        t('errors.noRole', 'Bu işlem için {{role}} rolü gereklidir.', { role }),
        [{ text: t('common.ok', 'Tamam') }]
      );
    }
    
    return hasAccess;
  };

  /**
   * Kullanıcının demo kullanıcı olup olmadığını kontrol eder
   * Türkçe açıklama: Demo kullanıcı kontrolü
   */
  const isDemoUser = user?.isDemo || false;

  /**
   * Kullanıcının gerçek kullanıcı olup olmadığını kontrol eder
   * Türkçe açıklama: Gerçek kullanıcı kontrolü
   */
  const isRealUser = !isDemoUser;

  /**
   * Kullanıcının aktif olup olmadığını kontrol eder
   * Türkçe açıklama: Kullanıcı durumu kontrolü
   */
  const isActiveUser = user?.isActive || false;

  // Rol kontrolü için kısayollar
  const isAdmin = hasRole('Admin');
  const isCashier = hasRole('Cashier');
  const isManager = hasRole('Manager');

  /**
   * Kullanıcının kritik işlemler yapma yetkisi var mı kontrol eder
   * Türkçe açıklama: Kritik işlem yetkisi kontrolü
   */
  const canPerformCriticalOperations = isAdmin || isManager;

  /**
   * Kullanıcının sistem ayarlarını değiştirme yetkisi var mı kontrol eder
   * Türkçe açıklama: Sistem ayarları yetkisi
   */
  const canManageSystemSettings = isAdmin;

  /**
   * Kullanıcının raporları görüntüleme yetkisi var mı kontrol eder
   * Türkçe açıklama: Rapor görüntüleme yetkisi
   */
  const canViewReports = isAdmin || isManager;

  /**
   * Kullanıcının kullanıcı yönetimi yapma yetkisi var mı kontrol eder
   * Türkçe açıklama: Kullanıcı yönetimi yetkisi
   */
  const canManageUsers = isAdmin;

  return {
    // Temel yetki kontrolü
    hasPermission,
    hasRole,
    hasAnyRole,
    hasAllRoles,
    
    // Güvenli erişim kontrolü
    checkPermissionWithAlert,
    checkRoleWithAlert,
    
    // Rol kısayolları
    isAdmin,
    isCashier,
    isManager,
    
    // Kullanıcı durumu
    isDemoUser,
    isRealUser,
    isActiveUser,
    
    // İşlem yetkileri
    canPerformCriticalOperations,
    canManageSystemSettings,
    canViewReports,
    canManageUsers,
    
    // Kullanıcı bilgileri
    user,
    userPermissions: Array.from(userPermissions),
    userRoles: Array.from(userRoles)
  };
}; 