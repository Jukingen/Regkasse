'use client';

import { ArrowLeftOutlined } from '@ant-design/icons';
import { Alert, Button, Space } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import { DigitalServiceAccess } from '@/features/digital/components/DigitalServiceAccess';
import { ManagerDigitalRequestPanel } from '@/features/digital/components/ManagerDigitalRequestPanel';
import { TenantDigitalServiceStatusPanel } from '@/features/digital/components/TenantDigitalServiceStatusPanel';
import {
  canGenerateDigitalApp,
  canGenerateDigitalWebsite,
} from '@/features/digital/digitalServicePermissions';
import { DigitalServicesPanel } from '@/features/website-generator/components/DigitalServicesPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

function TenantDigitalPageBody({ tenantId }: { tenantId: string }) {
  const { user, isSuperAdmin } = usePermissions();
  const userPerms = user ? { permissions: user.permissions } : null;
  const { data } = useTenantDigitalService(tenantId);

  // Manager (and any non–Super Admin): view / preview / request only — no create/generate UI.
  if (!isSuperAdmin) {
    return <ManagerDigitalRequestPanel tenantId={tenantId} />;
  }

  const websiteEnabled = canGenerateDigitalWebsite(userPerms, isSuperAdmin, data);
  const appEnabled = canGenerateDigitalApp(userPerms, isSuperAdmin, data);

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <TenantDigitalServiceStatusPanel tenantId={tenantId} />
      <DigitalServicesPanel
        tenantId={tenantId}
        websiteEnabled={websiteEnabled}
        appEnabled={appEnabled}
      />
    </Space>
  );
}

/**
 * Mandanten / Super Admin digital services for a specific tenant:
 * Manager: view + request (no create/generate); Super Admin: status + generators.
 */
export default function DigitalServicesPage() {
  const { t } = useI18n();
  const params = useParams();
  const tenantId = typeof params.id === 'string' ? params.id : '';

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <DigitalServiceAccess tenantId={tenantId} blockWhenDisabled={false}>
        <AdminPageHeader
          title={t('tenants.digitalServices.pageTitle')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: t('tenants.page.title'), href: '/admin/tenants' },
            { title: tenantId, href: `/admin/tenants/${tenantId}` },
            { title: t('tenants.digitalServices.pageTitle') },
          ]}
          actions={
            <Link href={`/admin/tenants/${tenantId}`}>
              <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
            </Link>
          }
        />
        <TenantDigitalPageBody tenantId={tenantId} />
      </DigitalServiceAccess>
    </AdminPageShell>
  );
}
