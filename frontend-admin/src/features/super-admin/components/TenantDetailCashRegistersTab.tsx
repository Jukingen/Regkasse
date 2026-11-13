'use client';

import { LoginOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import { dateColumnRender } from '@/components/DateColumn';
import {
  type AdminTenantCashRegister,
  listAdminTenantCashRegisters,
} from '@/features/super-admin/api/adminTenantCashRegisters';
import { registerStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';
import { useI18n } from '@/i18n';

export type TenantDetailCashRegistersTabProps = {
  tenantId: string;
  onImpersonate: () => void;
  impersonatePending?: boolean;
};

export function TenantDetailCashRegistersTab({
  tenantId,
  onImpersonate,
  impersonatePending,
}: TenantDetailCashRegistersTabProps) {
  const { t } = useI18n();

  const registersQuery = useQuery({
    queryKey: ['admin', 'tenant-cash-registers', tenantId],
    queryFn: () => listAdminTenantCashRegisters(tenantId),
  });

  const columns: ColumnsType<AdminTenantCashRegister> = [
    {
      title: t('tenants.detail.registers.columns.name'),
      dataIndex: 'location',
      key: 'location',
    },
    {
      title: t('tenants.detail.registers.columns.number'),
      dataIndex: 'registerNumber',
      key: 'registerNumber',
    },
    {
      title: t('tenants.columns.status'),
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <Tag color={registerStatusColor(status)}>{status}</Tag>,
    },
    {
      title: t('tenants.detail.registers.columns.lastUsed'),
      dataIndex: 'lastUsedAtUtc',
      key: 'lastUsedAtUtc',
      render: dateColumnRender('datetime'),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Alert type="info" showIcon title={t('tenants.detail.registers.hint')} />
      <Space wrap>
        <Button icon={<ReloadOutlined />} onClick={() => void registersQuery.refetch()}>
          {t('common.refresh')}
        </Button>
        <Button
          type="primary"
          icon={<LoginOutlined />}
          loading={impersonatePending}
          onClick={onImpersonate}
        >
          {t('tenants.detail.registers.addViaImpersonation')}
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={registersQuery.isLoading}
        dataSource={registersQuery.data ?? []}
        columns={columns}
        locale={{ emptyText: t('tenants.detail.registers.empty') }}
        pagination={{ pageSize: 20 }}
      />
    </Space>
  );
}
