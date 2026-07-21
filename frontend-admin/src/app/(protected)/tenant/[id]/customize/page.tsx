'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button, Tabs } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { TenantCustomizationPanel } from '@/features/website-generator/components/TenantCustomizationPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

/**
 * Super Admin website/app branding for a specific tenant.
 * Uses `/api/admin/tenant-customizations` with explicit `tenantId`.
 * Debounced live preview lives in TenantCustomizationPanel
 * (`POST /api/admin/website/preview` for website; local mock for app).
 */
export default function TenantCustomizePage() {
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
        title={t('tenants.customization.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('tenants.page.title'), href: '/admin/tenants' },
          { title: tenantId, href: `/admin/tenants/${tenantId}` },
          { title: t('tenants.customization.pageTitle') },
        ]}
        actions={
          <Link href={`/admin/tenants/${tenantId}`}>
            <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
          </Link>
        }
      />
      <Tabs
        items={[
          {
            key: 'website',
            label: t('tenants.customization.websiteTitle'),
            children: <TenantCustomizationPanel surface="website" tenantId={tenantId} showIntro />,
          },
          {
            key: 'app',
            label: t('tenants.customization.appTitle'),
            children: <TenantCustomizationPanel surface="app" tenantId={tenantId} showIntro />,
          },
        ]}
      />
    </AdminPageShell>
  );
}
