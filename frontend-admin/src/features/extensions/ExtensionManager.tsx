'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Switch, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';

import {
  setTenantExtension,
  tenantExtensionQueryKeys,
  useTenantExtensions,
  type TenantExtensionStatus,
} from '@/api/admin/tenantExtensions';
import { HAIR_SALON_CAMPAIGN_EXTENSION } from '@/api/admin/hairSalonCampaigns';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

const DESCRIPTION_KEYS: Record<string, string> = {
  [HAIR_SALON_CAMPAIGN_EXTENSION]: 'admin.extensions.descriptions.hair_salon_campaign',
};

export function extensionDescription(
  t: (key: string) => string,
  name: string
): string {
  const key = DESCRIPTION_KEYS[name];
  return key ? t(key) : name;
}

export function ExtensionManager() {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const extensions = useTenantExtensions();

  const toggle = useMutation({
    mutationFn: (body: { name: string; enabled: boolean }) => setTenantExtension(body),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantExtensionQueryKeys.all });
      notify.successKey('admin.extensions.saved');
    },
    onError: (err) =>
      notify.apiError(err, { logContext: 'ExtensionManager.toggle', fallbackKey: 'admin.extensions.saveError' }),
  });

  const columns: ColumnsType<TenantExtensionStatus> = [
    {
      title: t('admin.extensions.colName'),
      dataIndex: 'name',
      key: 'name',
    },
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
    {
      title: t('admin.extensions.colActions'),
      key: 'actions',
      render: (_value, row) => (
        <>
          <Switch
            checked={row.enabled}
            disabled={!canWrite || toggle.isPending}
            aria-label={row.name}
            onChange={(enabled) => toggle.mutate({ name: row.name, enabled })}
          />
          {row.name === HAIR_SALON_CAMPAIGN_EXTENSION && row.enabled ? (
            <Link href="/admin/extensions/hair-salon-campaigns" style={{ marginLeft: 12 }}>
              {t('admin.extensions.manageCampaigns')}
            </Link>
          ) : null}
        </>
      ),
    },
  ];

  if (extensions.isError) {
    return <Alert type="error" showIcon title={t('admin.extensions.loadError')} />;
  }

  return (
    <>
      {!canWrite ? (
        <Alert type="info" showIcon title={t('admin.extensions.readOnly')} style={{ marginBottom: 16 }} />
      ) : null}
      <Table
        rowKey="name"
        loading={extensions.isLoading}
        columns={columns}
        dataSource={extensions.data ?? []}
        pagination={false}
        locale={{ emptyText: t('admin.extensions.empty') }}
      />
    </>
  );
}
