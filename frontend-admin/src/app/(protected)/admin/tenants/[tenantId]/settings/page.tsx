'use client';

import { ArrowLeftOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Space } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { tenantSettingsQueryKeys } from '@/features/tenants/api/tenantSettings';
import { TenantSettingsChangePanel } from '@/features/tenants/components/TenantSettingsChangePanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function TenantSettingsPage() {
  const params = useParams();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const queryClient = useQueryClient();

  const canAccess = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
    { title: t('tenants.page.title'), href: '/admin/tenants' },
    {
      title: tenantId || t('tenants.settingsChange.pageTitle'),
      href: tenantId ? `/admin/tenants/${tenantId}` : '/admin/tenants',
    },
    { title: t('tenants.settingsChange.pageTitle') },
  ];

  if (!canAccess) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('tenants.settingsChange.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="error"
          showIcon
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  if (!tenantId) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('tenants.settingsChange.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('tenants.settingsChange.pageTitle')}
        breadcrumbs={breadcrumbs}
        actions={
          <Space wrap>
            <Link href={`/admin/tenants/${tenantId}`}>
              <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
            </Link>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void queryClient.invalidateQueries({
                  queryKey: tenantSettingsQueryKeys.root,
                });
              }}
            >
              {t('common.buttons.refresh')}
            </Button>
          </Space>
        }
      />
      <TenantSettingsChangePanel tenantId={tenantId} />
    </AdminPageShell>
  );
}
