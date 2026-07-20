'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Input, Space, Switch, Table, Typography } from 'antd';
import { useState } from 'react';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import {
  addTenantDomain,
  downloadTenantWebsitePackage,
  fetchTenantDomains,
  publishTenantSite,
  removeTenantDomain,
  setTenantDomainWebsiteEnabled,
  verifyTenantDomain,
  type TenantDomain,
} from '../api/tenantDomainsApi';

const { Paragraph, Text } = Typography;

export function TenantDomainsPanel() {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [domain, setDomain] = useState('');
  const [verifyToken, setVerifyToken] = useState<Record<string, string>>({});

  const domainsQuery = useQuery({
    queryKey: ['admin', 'tenant-domains'],
    queryFn: () => fetchTenantDomains(),
  });

  const invalidate = () =>
    void queryClient.invalidateQueries({ queryKey: ['admin', 'tenant-domains'] });

  const addMutation = useMutation({
    mutationFn: () => addTenantDomain(domain),
    onSuccess: () => {
      message.success(t('settings.websiteGenerator.domainAdded'));
      setDomain('');
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const verifyMutation = useMutation({
    mutationFn: ({ id, token }: { id: string; token: string }) => verifyTenantDomain(id, token),
    onSuccess: () => {
      message.success(t('settings.websiteGenerator.domainVerified'));
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setTenantDomainWebsiteEnabled(id, enabled),
    onSuccess: () => invalidate(),
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => removeTenantDomain(id),
    onSuccess: () => {
      message.success(t('settings.websiteGenerator.domainRemoved'));
      invalidate();
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const publishMutation = useMutation({
    mutationFn: () => publishTenantSite('modern'),
    onSuccess: (result) => {
      if (!result.succeeded) {
        message.error(result.error ?? t('common.errors.http500'));
        return;
      }
      message.success(t('settings.websiteGenerator.publishSuccess'));
      if (result.url) {
        modal.info({
          title: t('settings.websiteGenerator.publishTitle'),
          content: (
            <div>
              <Paragraph copyable>{result.url}</Paragraph>
              {result.customDomain ? (
                <Paragraph>
                  {t('settings.websiteGenerator.customDomainLabel')}: {result.customDomain}
                </Paragraph>
              ) : null}
            </div>
          ),
        });
      }
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const packageMutation = useMutation({
    mutationFn: () => downloadTenantWebsitePackage({ templateId: 'modern' }),
    onSuccess: () => {
      message.success(t('settings.websiteGenerator.packageSuccess'));
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantDomainsPanel' }),
  });

  const rows = domainsQuery.data ?? [];

  return (
    <Card title={t('settings.websiteGenerator.domainsTitle')} loading={domainsQuery.isLoading}>
      <Paragraph type="secondary">{t('settings.websiteGenerator.domainsIntro')}</Paragraph>

      <Space.Compact style={{ width: '100%', maxWidth: 480, marginBottom: 16 }}>
        <Input
          placeholder={t('settings.websiteGenerator.domainPlaceholder')}
          value={domain}
          onChange={(e) => setDomain(e.target.value)}
          onPressEnter={() => addMutation.mutate()}
        />
        <Button type="primary" loading={addMutation.isPending} onClick={() => addMutation.mutate()}>
          {t('settings.websiteGenerator.addDomain')}
        </Button>
      </Space.Compact>

      <Table<TenantDomain>
        rowKey="id"
        size="small"
        pagination={false}
        dataSource={rows}
        locale={{ emptyText: t('settings.websiteGenerator.noDomains') }}
        columns={[
          {
            title: t('settings.websiteGenerator.colDomain'),
            dataIndex: 'domain',
          },
          {
            title: t('settings.websiteGenerator.colVerified'),
            dataIndex: 'isVerified',
            render: (v: boolean) => (v ? t('common.buttons.yes') : t('common.buttons.no')),
          },
          {
            title: t('settings.websiteGenerator.colWebsite'),
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
            title: t('settings.websiteGenerator.colActions'),
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
                        placeholder={t('settings.websiteGenerator.verifyTokenPlaceholder')}
                        value={verifyToken[row.id] ?? ''}
                        onChange={(e) =>
                          setVerifyToken((prev) => ({ ...prev, [row.id]: e.target.value }))
                        }
                      />
                      <Button
                        size="small"
                        loading={verifyMutation.isPending}
                        onClick={() =>
                          verifyMutation.mutate({
                            id: row.id,
                            token: verifyToken[row.id] ?? row.verificationToken ?? '',
                          })
                        }
                      >
                        {t('settings.websiteGenerator.verify')}
                      </Button>
                    </Space.Compact>
                  </>
                ) : null}
                <Button
                  size="small"
                  danger
                  onClick={() =>
                    modal.confirm({
                      title: t('settings.websiteGenerator.removeDomainConfirm'),
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

      <Alert
        style={{ marginTop: 16 }}
        type="info"
        showIcon
        message={t('settings.websiteGenerator.publishHint')}
      />
      <Space style={{ marginTop: 12 }} wrap>
        <Button type="default" loading={publishMutation.isPending} onClick={() => publishMutation.mutate()}>
          {t('settings.websiteGenerator.publishLive')}
        </Button>
        <Button
          type="primary"
          loading={packageMutation.isPending}
          onClick={() => packageMutation.mutate()}
        >
          {t('settings.websiteGenerator.downloadPackage')}
        </Button>
      </Space>
    </Card>
  );
}
