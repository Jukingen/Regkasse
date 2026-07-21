'use client';

import { DownloadOutlined, MobileOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Space, Switch, Table, Typography } from 'antd';
import { useState } from 'react';

import {
  type TenantDomain,
  addTenantDomain,
  downloadTenantWebsitePackage,
  fetchTenantDomains,
  removeTenantDomain,
  setTenantDomainWebsiteEnabled,
  verifyTenantDomain,
} from '@/features/website-generator/api/tenantDomainsApi';
import { downloadTenantAppPackage } from '@/features/website-generator/api/websiteGeneratorApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Paragraph, Text } = Typography;

type TenantDomainManagementPanelProps = {
  /** When set (Super Admin), all domain APIs target this tenant. */
  tenantId: string;
};

/**
 * Super Admin / FA panel: custom domain + website ZIP + mobile app package.
 * Uses `/api/admin/tenant-domains` and `/api/admin/website/*` (not `/api/tenant/...`).
 */
export function TenantDomainManagementPanel({ tenantId }: TenantDomainManagementPanelProps) {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [domain, setDomain] = useState('');
  const [verifyToken, setVerifyToken] = useState<Record<string, string>>({});

  const queryKey = ['admin', 'tenant-domains', tenantId] as const;

  const domainsQuery = useQuery({
    queryKey,
    queryFn: () => fetchTenantDomains(tenantId),
    enabled: Boolean(tenantId),
  });

  const invalidate = () => void queryClient.invalidateQueries({ queryKey });

  const addMutation = useMutation({
    mutationFn: () => addTenantDomain(domain, tenantId),
    onSuccess: () => {
      message.success(t('tenants.domainManagement.domainAdded'));
      setDomain('');
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const verifyMutation = useMutation({
    mutationFn: ({ id, token }: { id: string; token: string }) =>
      verifyTenantDomain(id, token, tenantId),
    onSuccess: () => {
      message.success(t('tenants.domainManagement.domainVerified'));
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setTenantDomainWebsiteEnabled(id, enabled, tenantId),
    onSuccess: () => invalidate(),
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => removeTenantDomain(id, tenantId),
    onSuccess: () => {
      message.success(t('tenants.domainManagement.domainRemoved'));
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const packageMutation = useMutation({
    mutationFn: () =>
      downloadTenantWebsitePackage({
        tenantId,
        templateId: 'modern',
      }),
    onSuccess: () => message.success(t('tenants.domainManagement.packageSuccess')),
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const appMutation = useMutation({
    mutationFn: () => downloadTenantAppPackage({ appType: 'Native', tenantId }),
    onSuccess: () => message.success(t('tenants.domainManagement.appSuccess')),
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainManagementPanel' }),
  });

  const rows = domainsQuery.data ?? [];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('tenants.domainManagement.intro')}
      </Paragraph>

      <Card title={t('tenants.domainManagement.settingsTitle')} loading={domainsQuery.isLoading}>
        <Form layout="vertical" onFinish={() => addMutation.mutate()}>
          <Form.Item label={t('tenants.domainManagement.domainLabel')} style={{ maxWidth: 480 }}>
            <Space.Compact style={{ width: '100%' }}>
              <Input
                addonBefore="https://"
                placeholder={t('tenants.domainManagement.domainPlaceholder')}
                value={domain}
                onChange={(e) => setDomain(e.target.value)}
              />
              <Button type="primary" htmlType="submit" loading={addMutation.isPending}>
                {t('tenants.domainManagement.addDomain')}
              </Button>
            </Space.Compact>
          </Form.Item>
        </Form>

        <Table<TenantDomain>
          rowKey="id"
          size="small"
          pagination={false}
          dataSource={rows}
          locale={{ emptyText: t('tenants.domainManagement.noDomains') }}
          columns={[
            {
              title: t('tenants.domainManagement.colDomain'),
              dataIndex: 'domain',
            },
            {
              title: t('tenants.domainManagement.colVerified'),
              dataIndex: 'isVerified',
              render: (v: boolean) => (v ? t('common.buttons.yes') : t('common.buttons.no')),
            },
            {
              title: t('tenants.domainManagement.colWebsite'),
              key: 'website',
              render: (_, row) => (
                <Switch
                  checked={row.isActive}
                  disabled={!row.isVerified}
                  onChange={(enabled) => toggleMutation.mutate({ id: row.id, enabled })}
                />
              ),
            },
            {
              title: t('tenants.domainManagement.colActions'),
              key: 'actions',
              render: (_, row) => (
                <Space orientation="vertical" size={8}>
                  {!row.isVerified && row.verificationToken ? (
                    <>
                      <Text type="secondary" style={{ fontSize: 12 }}>
                        TXT: regkasse-verify={row.verificationToken}
                      </Text>
                      <Space.Compact>
                        <Input
                          size="small"
                          placeholder={t('tenants.domainManagement.verifyTokenPlaceholder')}
                          value={verifyToken[row.id] ?? ''}
                          onChange={(e) =>
                            setVerifyToken((prev) => ({ ...prev, [row.id]: e.target.value }))
                          }
                        />
                        <Button
                          size="small"
                          type="primary"
                          loading={verifyMutation.isPending}
                          onClick={() =>
                            verifyMutation.mutate({
                              id: row.id,
                              token: verifyToken[row.id] ?? row.verificationToken ?? '',
                            })
                          }
                        >
                          {t('tenants.domainManagement.verify')}
                        </Button>
                      </Space.Compact>
                    </>
                  ) : null}
                  <Button
                    size="small"
                    danger
                    onClick={() =>
                      modal.confirm({
                        title: t('tenants.domainManagement.removeConfirm'),
                        onOk: () => removeMutation.mutateAsync(row.id),
                      })
                    }
                  >
                    {t('common.buttons.delete')}
                  </Button>
                </Space>
              ),
            },
          ]}
        />
      </Card>

      <Card title={t('tenants.domainManagement.websiteCardTitle')}>
        <Paragraph type="secondary">{t('tenants.domainManagement.websiteHint')}</Paragraph>
        <Button
          type="primary"
          icon={<DownloadOutlined />}
          loading={packageMutation.isPending}
          onClick={() => packageMutation.mutate()}
        >
          {t('tenants.domainManagement.downloadPackage')}
        </Button>
      </Card>

      <Card title={t('tenants.domainManagement.appCardTitle')}>
        <Paragraph type="secondary">{t('tenants.domainManagement.appHint')}</Paragraph>
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('tenants.digitalServices.nativeDownloadHint')}
        />
        <Button
          type="primary"
          icon={<MobileOutlined />}
          loading={appMutation.isPending}
          onClick={() => appMutation.mutate()}
        >
          {t('tenants.domainManagement.downloadApp')}
        </Button>
      </Card>
    </Space>
  );
}
