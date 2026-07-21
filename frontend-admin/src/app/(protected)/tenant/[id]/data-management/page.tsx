'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { TenantDataManagementPanel } from '@/features/data-management/components/TenantDataManagementPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

/**
 * GDPR customer data rights + expired-license data management:
 * View / Export / Delete requests, inventory, RKSV retention.
 */
export default function TenantDataManagementPage() {
  const { t } = useI18n();
  const params = useParams();
  const tenantId = typeof params.id === 'string' ? params.id : '';
  const { user, isSuperAdmin } = usePermissions();
  const allowed =
    isSuperAdmin ||
    hasPermission(user ? { permissions: user.permissions } : null, PERMISSIONS.BACKUP_MANAGE);

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

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

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('dataManagement.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('tenants.page.title'), href: '/admin/tenants' },
          { title: tenantId, href: `/admin/tenants/${tenantId}` },
          { title: t('dataManagement.title') },
        ]}
        actions={
          <Link href={`/admin/tenants/${tenantId}`}>
            <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
          </Link>
        }
      />
      <TenantDataManagementPanel tenantId={tenantId} />
    </AdminPageShell>
  );
}
