'use client';

import { Alert, Typography } from 'antd';
import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { TenantVerticalProfileEditor } from '@/features/super-admin/components/TenantVerticalProfileEditor';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function TenantVerticalProfilePage() {
  const params = useParams();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canAccess = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'administration', [
    { title: t('tenants.page.title'), href: '/admin/tenants' },
    { title: t('tenants.verticalProfile.pageTitle') },
  ]);

  if (!canAccess) {
    return (
      <AdminPageShell>
        <AdminPageHeader
          title={t('tenants.verticalProfile.pageTitle')}
          breadcrumbs={breadcrumbs}
        />
        <Alert
          type="error"
          showIcon
          title={t('tenants.verticalProfile.accessDenied')}
        />
      </AdminPageShell>
    );
  }

  if (!tenantId) {
    return (
      <AdminPageShell>
        <AdminPageHeader
          title={t('tenants.verticalProfile.pageTitle')}
          breadcrumbs={breadcrumbs}
        />
        <Alert
          type="error"
          showIcon
          title={t('tenants.users.errors.invalidTenant')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('tenants.verticalProfile.pageTitle')}
        breadcrumbs={breadcrumbs}
      />
      <Typography.Paragraph type="secondary">
        {t('tenants.verticalProfile.subtitle')}
      </Typography.Paragraph>
      <TenantVerticalProfileEditor tenantId={tenantId} />
    </AdminPageShell>
  );
}
