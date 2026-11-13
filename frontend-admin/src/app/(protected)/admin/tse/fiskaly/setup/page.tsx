'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Space, Table, Tag, Typography } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getFiskalySetup,
  isFiskalyFonAuthenticated,
  isFiskalyResourceInitialized,
} from '@/features/fiskaly/api/fiskalySetup';
import { FiskalySetupWizard } from '@/features/fiskaly/components/FiskalySetupWizard';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function FiskalySetupPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { isReady } = useTsePageTenant();

  const query = useQuery({
    queryKey: ['admin', 'fiskaly', 'setup'],
    queryFn: ({ signal }) => getFiskalySetup(signal),
    enabled: allowed,
    staleTime: 10_000,
  });

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseFiskaly.forbidden')} />;
  }

  const status = query.data;
  const setupComplete =
    isFiskalyFonAuthenticated(status?.fon) && isFiskalyResourceInitialized(status?.scu.state);

  return (
    <div>
      <AdminPageHeader
        title={t('tseFiskaly.setup.pageTitle')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', [
          { title: t('tseFiskaly.title'), href: '/admin/tse/fiskaly' },
          { title: t('tseFiskaly.setup.pageTitle') },
        ])}
        extra={<TseActiveTenantTag />}
      />
      <Typography.Paragraph type="secondary">{t('tseFiskaly.setup.pageSubtitle')}</Typography.Paragraph>

      {!isReady ? <TseTenantRequiredAlert emptySelectKey="tseFiskaly.setup.tenantRequired" /> : null}

      {query.isError ? (
        <Alert type="error" showIcon title={t('tseFiskaly.setup.loadFailed')} style={{ marginBottom: 16 }} />
      ) : null}

      <Card loading={query.isLoading} style={{ marginBottom: 16 }}>
        <Descriptions column={1} size="small">
          <Descriptions.Item label={t('tseFiskaly.environmentLabel')}>
            {status?.environment ?? 'TEST'}
          </Descriptions.Item>
          <Descriptions.Item label={t('tseFiskaly.setup.fonStatus')}>
            <Tag color={isFiskalyFonAuthenticated(status?.fon) ? 'green' : 'orange'}>
              {status?.fon.authenticationStatus ?? t('tseFiskaly.setup.unknown')}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('tseFiskaly.setup.scuStatus')}>
            <Tag color={isFiskalyResourceInitialized(status?.scu.state) ? 'green' : 'orange'}>
              {status?.scu.state ?? t('tseFiskaly.setup.unknown')}
            </Tag>
          </Descriptions.Item>
        </Descriptions>
        <Space style={{ marginTop: 12 }}>
          <Link href="/admin/tse/fiskaly">{t('tseFiskaly.setup.backToSettings')}</Link>
        </Space>
      </Card>

      {query.isLoading ? null : setupComplete ? (
        <Card title={t('tseFiskaly.setup.dashboardTitle')}>
          <Table
            rowKey="cashRegisterId"
            size="small"
            pagination={false}
            dataSource={status?.cashRegisters ?? []}
            columns={[
              {
                title: t('tseFiskaly.setup.colRegister'),
                dataIndex: 'registerNumber',
              },
              {
                title: t('tseFiskaly.setup.colLocation'),
                dataIndex: 'location',
              },
              {
                title: t('tseFiskaly.setup.colState'),
                dataIndex: 'state',
                render: (state: string) => (
                  <Tag color={isFiskalyResourceInitialized(state) ? 'green' : 'orange'}>{state}</Tag>
                ),
              },
            ]}
            locale={{ emptyText: t('tseFiskaly.setup.noCashRegisters') }}
          />
          {(status?.cashRegisters.some((r) => !isFiskalyResourceInitialized(r.state)) ?? false) ? (
            <div style={{ marginTop: 16 }}>
              <Typography.Paragraph>{t('tseFiskaly.setup.remainingHint')}</Typography.Paragraph>
              <FiskalySetupWizard status={status} />
            </div>
          ) : (
            <Alert type="success" showIcon style={{ marginTop: 16 }} title={t('tseFiskaly.setup.completeTitle')} />
          )}
          <div style={{ marginTop: 16 }}>
            <Link href="/admin/tse/fiskaly/test">
              <Button>{t('tseFiskaly.test.openPage')}</Button>
            </Link>
          </div>
        </Card>
      ) : (
        <FiskalySetupWizard status={status} />
      )}
    </div>
  );
}
