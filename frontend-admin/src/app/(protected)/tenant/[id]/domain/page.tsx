'use client';

import { Alert, Button } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { TenantDomainManagementPanel } from '@/features/website-generator/components/TenantDomainManagementPanel';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Super Admin custom-domain management for a specific tenant.
 * Branding lives under `/tenant/[id]/customize`.
 */
export default function TenantDomainPage() {
  const { t } = useI18n();
  const params = useParams();
  const { user } = useAuth();
  const tenantId = typeof params.id === 'string' ? params.id : '';

  const canAccess =
    isSuperAdmin(user?.role) ||
    hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL) ||
    hasPermission(user, PERMISSIONS.WEBSITE_MANAGE);

  if (!canAccess) {
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

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('tenants.domainManagement.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('tenants.page.title'), href: '/admin/tenants' },
          { title: tenantId, href: `/admin/tenants/${tenantId}` },
          { title: t('tenants.domainManagement.pageTitle') },
        ]}
        actions={
          <>
            <Link href={`/tenant/${tenantId}/customize`}>
              <Button>{t('tenants.customization.openAction')}</Button>
            </Link>
            <Link href={`/admin/tenants/${tenantId}`}>
              <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
            </Link>
          </>
        }
      />
      <TenantDomainManagementPanel tenantId={tenantId} />
    </AdminPageShell>
  );
}
