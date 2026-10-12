'use client';

import { Alert, Card, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import { useTenantExtensions, type TenantExtensionStatus } from '@/api/admin/tenantExtensions';
import { extensionDescription } from '@/features/extensions/ExtensionManager';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export function TenantExtensionsPanel({ tenantId }: { tenantId: string }) {
  const { t } = useI18n();
  const ambient = useCurrentTenant();
  const matches = Boolean(tenantId && ambient.tenantId && ambient.tenantId === tenantId);
  const extensions = useTenantExtensions({ enabled: matches });

  const columns: ColumnsType<TenantExtensionStatus> = [
    { title: t('admin.extensions.colName'), dataIndex: 'name', key: 'name' },
    {
      title: t('admin.extensions.colDescription'),
      key: 'description',
      render: (_value, row) => extensionDescription(t, row.name),
    },
    {
      title: t('admin.extensions.colStatus'),
      dataIndex: 'enabled',
      key: 'status',
      render: (enabled: boolean) => (
        <Tag color={enabled ? 'green' : 'default'}>
          {enabled ? t('admin.extensions.enabled') : t('admin.extensions.disabled')}
        </Tag>
      ),
    },
  ];

  return (
    <Card title={t('admin.extensions.profileTitle')} style={{ marginTop: 24 }}>
      {matches ? (
        <>
          <Typography.Paragraph type="secondary">
            {t('admin.extensions.profileSubtitle', {
              tenant: ambient.tenantName ?? ambient.tenantId ?? '',
            })}
          </Typography.Paragraph>
          {extensions.isError ? (
            <Alert type="error" showIcon title={t('admin.extensions.loadError')} />
          ) : (
            <Table
              rowKey="name"
              loading={extensions.isLoading}
              columns={columns}
              dataSource={extensions.data ?? []}
              pagination={false}
              locale={{ emptyText: t('admin.extensions.profileEmpty') }}
            />
          )}
        </>
      ) : (
        <Alert type="info" showIcon title={t('admin.extensions.profileMismatch')} />
      )}
    </Card>
  );
}
