'use client';

import { Alert, Button, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import { DateColumn } from '@/components/DateColumn';
import type { CashRegisterOpenRequest } from '@/features/cash-registers/api/openRequests';
import {
  useApproveCashRegisterOpenRequest,
  useCashRegisterOpenRequests,
  useDenyCashRegisterOpenRequest,
} from '@/features/cash-registers/hooks/useCashRegisterOpenRequests';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const { Paragraph, Title } = Typography;

type CashRegisterOpenRequestsPanelProps = {
  tenantId?: string;
  titleLevel?: 4 | 5;
};

export function CashRegisterOpenRequestsPanel({
  tenantId,
  titleLevel = 5,
}: CashRegisterOpenRequestsPanelProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { data, isLoading, isError } = useCashRegisterOpenRequests('Pending', tenantId);
  const approveMutation = useApproveCashRegisterOpenRequest();
  const denyMutation = useDenyCashRegisterOpenRequest();

  const resolveRequest = async (id: string, action: 'approve' | 'deny') => {
    try {
      if (action === 'approve') {
        await approveMutation.mutateAsync({ id });
        notify.successKey('cashRegisters.openRequests.approved');
      } else {
        await denyMutation.mutateAsync({ id });
        notify.successKey('cashRegisters.openRequests.denied');
      }
    } catch (err) {
      notify.apiError(err, {
        fallbackKey: 'cashRegisters.openRequests.resolveFailed',
        logContext: 'CashRegisterOpenRequestsPanel.resolve',
      });
    }
  };

  const columns: ColumnsType<CashRegisterOpenRequest> = [
    {
      title: t('cashRegisters.openRequests.columns.register'),
      key: 'register',
      render: (_, row) => (
        <Space orientation="vertical" size={0}>
          <Typography.Text strong>{row.registerNumber || row.cashRegisterId}</Typography.Text>
          {row.location ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.location}
            </Typography.Text>
          ) : null}
        </Space>
      ),
    },
    {
      title: t('cashRegisters.openRequests.columns.cashier'),
      key: 'cashier',
      render: (_, row) => row.requestedByUserName || row.requestedByUserId || '—',
    },
    {
      title: t('cashRegisters.openRequests.columns.requestedAt'),
      key: 'requestedAt',
      render: (_, row) => <DateColumn date={row.requestedAt} format="datetime" />,
    },
    {
      title: t('cashRegisters.openRequests.columns.status'),
      key: 'status',
      render: () => (
        <Tag color="processing">{t('cashRegisters.openRequests.status.pending')}</Tag>
      ),
    },
    {
      title: t('cashRegisters.openRequests.columns.actions'),
      key: 'actions',
      render: (_, row) =>
        row.status === 'Pending' ? (
          <Space>
            <Button
              type="primary"
              size="small"
              loading={approveMutation.isPending}
              onClick={() => void resolveRequest(row.id, 'approve')}
            >
              {t('cashRegisters.openRequests.approve')}
            </Button>
            <Button
              danger
              size="small"
              loading={denyMutation.isPending}
              onClick={() => void resolveRequest(row.id, 'deny')}
            >
              {t('cashRegisters.openRequests.deny')}
            </Button>
          </Space>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
  ];

  const rows = data ?? [];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%', marginBottom: 16 }}>
      <Title level={titleLevel} style={{ margin: 0 }}>
        {t('cashRegisters.openRequests.title')}
      </Title>
      <Paragraph type="secondary" style={{ margin: 0 }}>
        {t('cashRegisters.openRequests.hint')}
      </Paragraph>
      {isError ? (
        <Alert type="error" showIcon title={t('cashRegisters.openRequests.loadFailed')} />
      ) : null}
      <Table
        rowKey="id"
        size="small"
        loading={isLoading}
        columns={columns}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('cashRegisters.openRequests.empty') }}
      />
    </Space>
  );
}
