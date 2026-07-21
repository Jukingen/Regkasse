'use client';

import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Empty, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnType } from 'antd/es/table';
import React from 'react';

import type { PaymentMethodDefinitionAdmin } from '@/api/admin/payment-method-definitions';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';

type PaymentMethodRegisterPanelProps = {
  registers: AdminCashRegisterListItem[];
  registersLoading: boolean;
  cashRegisterId: string | null;
  onSelectRegister: (registerId: string) => void;
  rows: PaymentMethodDefinitionAdmin[];
  tableLoading: boolean;
  canManage: boolean;
  onAdd: () => void;
  onEdit: (row: PaymentMethodDefinitionAdmin) => void;
  onDeactivate: (row: PaymentMethodDefinitionAdmin) => void;
};

export function PaymentMethodRegisterPanel({
  registers,
  registersLoading,
  cashRegisterId,
  onSelectRegister,
  rows,
  tableLoading,
  canManage,
  onAdd,
  onEdit,
  onDeactivate,
}: PaymentMethodRegisterPanelProps) {
  const { t } = useI18n();

  const registerOptions = registers.map((r) => ({
    value: r.id,
    label: `${r.registerNumber}${r.location ? ` — ${r.location}` : ''}`,
  }));

  const selectedRegister = registers.find((r) => r.id === cashRegisterId);

  const columns: ColumnType<PaymentMethodDefinitionAdmin>[] = [
    {
      title: t('settings.paymentMethods.columns.code'),
      dataIndex: 'code',
      key: 'code',
      width: 140,
    },
    { title: t('settings.paymentMethods.columns.name'), dataIndex: 'name', key: 'name' },
    {
      title: t('settings.paymentMethods.columns.active'),
      dataIndex: 'isActive',
      key: 'isActive',
      width: 100,
      render: (v: boolean) =>
        v ? (
          <Tag color="green">{t('common.buttons.yes')}</Tag>
        ) : (
          <Tag>{t('common.buttons.no')}</Tag>
        ),
    },
    {
      title: t('settings.paymentMethods.columns.default'),
      dataIndex: 'isDefault',
      key: 'isDefault',
      width: 90,
      render: (v: boolean) =>
        v ? <Tag color="blue">{t('common.buttons.yes')}</Tag> : <Tag>{t('common.buttons.no')}</Tag>,
    },
    {
      title: t('settings.paymentMethods.columns.order'),
      dataIndex: 'displayOrder',
      key: 'displayOrder',
      width: 90,
    },
    {
      title: t('settings.paymentMethods.columns.legacy'),
      dataIndex: 'legacyPaymentMethodValue',
      key: 'legacyPaymentMethodValue',
      width: 110,
    },
    {
      title: t('settings.paymentMethods.columns.terminal'),
      key: 'term',
      width: 120,
      render: (_, r) =>
        r.requiresTerminal ? r.terminalType?.trim() || FORMAT_EMPTY_DISPLAY : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('settings.paymentMethods.columns.actions'),
      key: 'actions',
      width: 200,
      render: (_, row) => (
        <Space>
          {canManage && (
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => onEdit(row)}>
              {t('common.buttons.edit')}
            </Button>
          )}
          {canManage && row.isActive && (
            <Button type="link" size="small" danger onClick={() => onDeactivate(row)}>
              {t('settings.paymentMethods.deactivate')}
            </Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('settings.paymentMethods.manageIntro')}
      </Typography.Paragraph>

      <Space wrap align="center">
        <Typography.Text strong>{t('settings.paymentMethods.cashRegister')}:</Typography.Text>
        <Select
          style={{ minWidth: 300 }}
          loading={registersLoading}
          placeholder={t('settings.paymentMethods.cashRegisterPlaceholder')}
          options={registerOptions}
          value={cashRegisterId ?? undefined}
          onChange={onSelectRegister}
        />
        {selectedRegister && (
          <Typography.Text type="secondary">
            {t('settings.paymentMethods.manageRegisterHint', {
              count: rows.filter((r) => r.isActive).length,
            })}
          </Typography.Text>
        )}
      </Space>

      {canManage && cashRegisterId && (
        <Button type="primary" icon={<PlusOutlined />} onClick={onAdd}>
          {t('settings.paymentMethods.add')}
        </Button>
      )}

      {!cashRegisterId ? (
        <Empty description={t('settings.paymentMethods.noCashRegister')} />
      ) : (
        <Table<PaymentMethodDefinitionAdmin>
          rowKey="id"
          loading={tableLoading}
          dataSource={rows}
          columns={columns}
          pagination={{ pageSize: 20 }}
          locale={{
            emptyText: (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('settings.paymentMethods.tableEmpty')}
              />
            ),
          }}
        />
      )}
    </Space>
  );
}
