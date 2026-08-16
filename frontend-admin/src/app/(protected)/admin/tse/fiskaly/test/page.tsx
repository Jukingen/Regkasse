'use client';

import { Alert, Card, Space, Typography } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { FiskalySignTestPanel } from '@/features/fiskaly/components/FiskalySignTestPanel';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function FiskalySignTestPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { isReady } = useTsePageTenant();

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseFiskaly.forbidden')} />;
  }

  return (
    <div>
      <AdminPageHeader
        title={t('tseFiskaly.test.pageTitle')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', [
          { title: t('tseFiskaly.title'), href: '/admin/tse/fiskaly' },
          { title: t('tseFiskaly.test.pageTitle') },
        ])}
        extra={<TseActiveTenantTag />}
      />
      <Typography.Paragraph type="secondary">{t('tseFiskaly.test.pageSubtitle')}</Typography.Paragraph>
      {!isReady ? <TseTenantRequiredAlert emptySelectKey="tseFiskaly.setup.tenantRequired" /> : null}
      {isReady ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Card size="small">
            <Link href="/admin/tse/fiskaly/setup">{t('tseFiskaly.test.backToSetup')}</Link>
          </Card>
          <FiskalySignTestPanel />
        </Space>
      ) : null}
    </div>
  );
}
