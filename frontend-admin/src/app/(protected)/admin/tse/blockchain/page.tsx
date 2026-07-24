'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Card,
  Divider,
  Form,
  Input,
  Select,
  Space,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getTseBlockchainStatus,
  listTseBlockchainTransactions,
  storeTseBlockchainSignature,
  syncTseBlockchain,
} from '@/features/tse-blockchain/api/blockchain';
import type { TseBlockchainTransaction } from '@/features/tse-blockchain/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-blockchain'] as const;

type AnchorForm = {
  signatureData: string;
};

export default function TseBlockchainPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [form] = Form.useForm<AnchorForm>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-blockchain'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const statusQuery = useQuery({
    queryKey: [...KEY, 'status'],
    queryFn: ({ signal }) => getTseBlockchainStatus(signal),
    enabled: allowed,
  });

  const txQuery = useQuery({
    queryKey: [...KEY, 'tx', tenantId],
    queryFn: ({ signal }) => listTseBlockchainTransactions(tenantId!, 50, signal),
    enabled: allowed && !!tenantId,
  });

  const syncMutation = useMutation({
    mutationFn: () => syncTseBlockchain(),
    onSuccess: async () => {
      notify.success(t('tseBlockchain.syncSuccess'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseBlockchain.sync',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const storeMutation = useMutation({
    mutationFn: (values: AnchorForm) =>
      storeTseBlockchainSignature({
        tenantId: tenantId!,
        signatureData: values.signatureData.trim(),
        sourceType: 'ManualSample',
      }),
    onSuccess: async () => {
      notify.success(t('tseBlockchain.anchorSuccess'));
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseBlockchain.store',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const columns: ColumnsType<TseBlockchainTransaction> = [
    {
      title: t('tseBlockchain.colTxHash'),
      dataIndex: 'transactionHash',
      key: 'transactionHash',
      ellipsis: true,
      render: (v: string) => (
        <Typography.Text code copyable={{ text: v }}>
          {v.length > 20 ? `${v.slice(0, 12)}…${v.slice(-8)}` : v}
        </Typography.Text>
      ),
    },
    {
      title: t('tseBlockchain.colBlock'),
      dataIndex: 'blockNumber',
      key: 'blockNumber',
      width: 100,
    },
    {
      title: t('tseBlockchain.colStatus'),
      dataIndex: 'isVerified',
      key: 'isVerified',
      width: 120,
      render: (v: boolean) =>
        v ? t('tseBlockchain.verified') : t('tseBlockchain.unverified'),
    },
    {
      title: t('tseBlockchain.colCreated'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm:ss'),
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseBlockchain.forbidden')} />;
  }

  const status = statusQuery.data;
  const connected = status?.blockchainStatus === 'connected';

  return (
    <div className="space-y-4">
      <AdminPageHeader
        title={t('tseBlockchain.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseBlockchain.title') }]}
        extra={
          <Select
            showSearch
            allowClear
            style={{ minWidth: 260 }}
            placeholder={t('tseBlockchain.tenantLabel')}
            value={tenantId}
            onChange={(v) => setTenantId(v)}
            options={(tenantsQuery.data ?? []).map((ten) => ({
              value: ten.id,
              label: ten.name ? `${ten.name} (${ten.slug})` : ten.slug,
            }))}
            optionFilterProp="label"
          />
        }
      />

      <Typography.Paragraph type="secondary">{t('tseBlockchain.subtitle')}</Typography.Paragraph>
      <Alert type="info" showIcon message={t('tseBlockchain.diagnosticNote')} />

      {statusQuery.isError ? (
        <Alert type="error" showIcon message={t('tseBlockchain.loadError')} />
      ) : (
        <Card title={t('tseBlockchain.cardTitle')} loading={statusQuery.isLoading}>
          <div className="flex flex-wrap items-center gap-4">
            <Badge
              status={connected ? 'success' : 'error'}
              text={
                connected
                  ? t('tseBlockchain.statusConnected')
                  : t('tseBlockchain.statusDisconnected')
              }
            />
            <span>
              {t('tseBlockchain.network')}: {status?.networkName ?? '—'}
            </span>
            <span>
              {t('tseBlockchain.block')}: {status?.currentBlock ?? '—'}
            </span>
            <span>
              {t('tseBlockchain.transactions')}: {status?.totalTransactions ?? '—'}
            </span>
          </div>

          <Divider />

          <Space wrap>
            <Button
              type="primary"
              loading={syncMutation.isPending}
              onClick={() => syncMutation.mutate()}
            >
              {t('tseBlockchain.sync')}
            </Button>
          </Space>

          <Divider />

          {!tenantId ? (
            <Alert type="info" showIcon message={t('tseBlockchain.emptySelect')} />
          ) : (
            <>
              <Form
                form={form}
                layout="inline"
                className="mb-4"
                onFinish={(values) => storeMutation.mutate(values)}
              >
                <Form.Item
                  name="signatureData"
                  rules={[{ required: true }]}
                  style={{ flex: 1, minWidth: 280 }}
                >
                  <Input placeholder={t('tseBlockchain.anchorPlaceholder')} />
                </Form.Item>
                <Form.Item>
                  <Button
                    htmlType="submit"
                    loading={storeMutation.isPending}
                    disabled={!tenantId}
                  >
                    {t('tseBlockchain.anchorSample')}
                  </Button>
                </Form.Item>
              </Form>

              <Table
                rowKey="id"
                size="small"
                loading={txQuery.isLoading}
                dataSource={txQuery.data ?? []}
                columns={columns}
                pagination={false}
                locale={{ emptyText: t('tseBlockchain.emptyTx') }}
              />
            </>
          )}
        </Card>
      )}
    </div>
  );
}
