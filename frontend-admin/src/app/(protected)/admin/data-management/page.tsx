'use client';

import { Alert } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { SuperAdminDataManagementDashboard } from '@/features/data-management/components/SuperAdminDataManagementDashboard';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

/**
 * Super Admin cross-tenant data management: lifecycle, grace/lock, deletion requests, RKSV retention.
 */
export default function AdminDataManagementPage() {
  const { t } = useI18n();
  const { isSuperAdmin } = usePermissions();

  if (!isSuperAdmin) {
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

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('dataManagement.admin.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.platformAdminHub'), href: '/admin' },
          { title: t('dataManagement.admin.pageTitle') },
        ]}
      />
      <SuperAdminDataManagementDashboard />
    </AdminPageShell>
  );
}
