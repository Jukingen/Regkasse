import { useContext } from 'react';

import { AuthContext } from '../contexts/AuthContext';
import { UsePermissionReturn } from '../types/auth';

/**
 * Yetki kontrol hook'u - Rol ve kaynak bazlı erişim kontrolü sağlar
 */
export const usePermission = (): UsePermissionReturn => {
  const { user } = useContext(AuthContext);

  /**
   * Kullanıcının belirli bir kaynak üzerinde belirli bir işlem yapma yetkisi var mı kontrol eder
   */
  const hasPermission = (resource: string, action: string): boolean => {
    if (!user?.permissions) return false;
    
    const requiredPermission = `${resource}.${action}`;
    return user.permissions.includes(requiredPermission);
  };

  /**
   * Kullanıcının belirli bir role sahip olup olmadığını kontrol eder
   */
  const hasRole = (role: string): boolean => {
    if (!user) return false;
    return user.role === role;
  };

  /**
   * Kullanıcının admin olup olmadığını kontrol eder
   */
  const isAdmin = hasRole('Admin');

  /**
   * Kullanıcının kasiyer olup olmadığını kontrol eder
   */
  const isCashier = hasRole('Cashier');

  /**
   * Kullanıcının yönetici olup olmadığını kontrol eder
   */
  const isManager = hasRole('Manager');

  return {
    hasPermission,
    hasRole,
    isAdmin,
    isCashier,
    isManager
  };
}; 