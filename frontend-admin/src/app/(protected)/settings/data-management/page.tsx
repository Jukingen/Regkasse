'use client';

import { useEffect } from 'react';
import { Alert } from 'antd';
import { useRouter } from 'next/navigation';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { PageSkeleton } from '@/components/Skeleton';
import { useCurrentTenant } from '@/hooks/useCurrentTenant';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

/**
 * Manager shortcut: redirects to ambient tenant data-management page.
 */
export default function SettingsDataManagementRedirectPage() {
  const { t } = useI18n();
  const router = useRouter();
  const { tenantId, isRealTenantSlug } = useCurrentTenant();
  const { user, isSuperAdmin } = usePermissions();
  const allowed =
    isSuperAdmin ||
    hasPermission(user ? { permissions: user.permissions } : null, PERMISSIONS.BACKUP_MANAGE);

  useEffect(() => {
    if (!allowed) return;
    if (tenantId && isRealTenantSlug) {
      router.replace(`/tenant/${tenantId}/data-management`);
    }
  }, [allowed, tenantId, isRealTenantSlug, router]);

  if (!allowed) {
    return (
      <AdminPageShell>
        <Alert
          type="error"
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  if (!tenantId || !isRealTenantSlug) {
    return (
      <AdminPageShell>
        <Alert type="warning" title={t('dataManagement.noTenantContext')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <PageSkeleton widgets={2} />
    </AdminPageShell>
  );
}
