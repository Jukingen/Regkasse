import React from 'react';

import { usePermission } from '../hooks/usePermission';
import { RoleGuardProps } from '../types/auth';

/**
 * Rol tabanlı erişim kontrol bileşeni - Belirli bir role sahip kullanıcılar için içerik gösterir
 */
export const RoleGuard: React.FC<RoleGuardProps> = ({ role, children, fallback }) => {
  const { hasRole } = usePermission();

  if (!hasRole(role)) {
    return fallback ? (
      <>{fallback}</>
    ) : (
      <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
        <p className="text-red-800 font-medium">
          Sie haben keine Berechtigung für diese Aktion.
        </p>
        <p className="text-red-600 text-sm mt-1">
          Erforderliche Rolle: {role}
        </p>
      </div>
    );
  }

  return <>{children}</>;
};

/**
 * Yetki tabanlı erişim kontrol bileşeni - Belirli bir yetkiye sahip kullanıcılar için içerik gösterir
 */
export const PermissionGuard: React.FC<{
  resource: string;
  action: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}> = ({ resource, action, children, fallback }) => {
  const { hasPermission } = usePermission();

  if (!hasPermission(resource, action)) {
    return fallback ? (
      <>{fallback}</>
    ) : (
      <div className="p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
        <p className="text-yellow-800 font-medium">
          Sie haben keine Berechtigung für diese Aktion.
        </p>
        <p className="text-yellow-600 text-sm mt-1">
          Erforderliche Berechtigung: {action} auf {resource}
        </p>
      </div>
    );
  }

  return <>{children}</>;
};

/**
 * Admin erişim kontrol bileşeni - Sadece admin kullanıcılar için içerik gösterir
 */
export const AdminOnly: React.FC<{ children: React.ReactNode; fallback?: React.ReactNode }> = ({ 
  children, 
  fallback 
}) => {
  return (
    <RoleGuard role="Admin" fallback={fallback}>
      {children}
    </RoleGuard>
  );
};

/**
 * Kasiyer erişim kontrol bileşeni - Kasiyer ve üstü kullanıcılar için içerik gösterir
 */
export const CashierOrHigher: React.FC<{ children: React.ReactNode; fallback?: React.ReactNode }> = ({ 
  children, 
  fallback 
}) => {
  const { isCashier, isAdmin, isManager } = usePermission();

  if (!isCashier && !isAdmin && !isManager) {
    return fallback ? (
      <>{fallback}</>
    ) : (
      <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-blue-800 font-medium">
          Diese Funktion ist nur für Kassierer und höhere Rollen verfügbar.
        </p>
      </div>
    );
  }

  return <>{children}</>;
}; 