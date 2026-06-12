'use client';

import React, { useMemo } from 'react';
import {
  Button,
  Card,
  Flex,
  Popover,
  Space,
  Spin,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleFilled,
  CloseCircleFilled,
  EditOutlined,
  MinusCircleOutlined,
  StarFilled,
} from '@ant-design/icons';

import type { PaymentMethodDefinitionAdmin } from '@/api/admin/payment-method-definitions';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useI18n } from '@/i18n';
import {
  buildPaymentMethodMatrix,
  type PaymentMethodMatrixRow,
  type PaymentMethodRegisterSummary,
} from '@/features/payment-methods/utils/buildPaymentMethodMatrix';

type PaymentMethodMatrixOverviewProps = {
  registers: AdminCashRegisterListItem[];
  methodsByRegisterId: Record<string, PaymentMethodDefinitionAdmin[] | undefined>;
  loading: boolean;
  canManage: boolean;
  onManageRegister: (registerId: string) => void;
  onEditDefinition: (definition: PaymentMethodDefinitionAdmin) => void;
  onToggleActive: (definition: PaymentMethodDefinitionAdmin, nextActive: boolean) => void;
};

function RegisterSummaryCard({
  summary,
  onClick,
}: {
  summary: PaymentMethodRegisterSummary;
  onClick: () => void;
}) {
  const { t } = useI18n();
  const title = summary.location
    ? `${summary.registerNumber} — ${summary.location}`
    : summary.registerNumber;

  return (
    <Card
      size="small"
      hoverable
      onClick={onClick}
      style={{ minWidth: 220, cursor: 'pointer' }}
      title={title}
      extra={
        summary.defaultCode ? (
          <Tooltip title={t('settings.paymentMethods.matrix.defaultBadge', { code: summary.defaultCode })}>
            <StarFilled style={{ color: '#1677ff' }} />
          </Tooltip>
        ) : null
      }
    >
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.paymentMethods.matrix.posOpen')}
          </Typography.Text>
          <div style={{ marginTop: 4 }}>
            {summary.activeCodes.length > 0 ? (
              summary.activeCodes.map((code) => (
                <Tag key={code} color="success" style={{ marginBottom: 4 }}>
                  {code}
                </Tag>
              ))
            ) : (
              <Typography.Text type="secondary">{t('settings.paymentMethods.matrix.noneActive')}</Typography.Text>
            )}
          </div>
        </div>
        {summary.inactiveCodes.length > 0 && (
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('settings.paymentMethods.matrix.posClosed')}
            </Typography.Text>
            <div style={{ marginTop: 4 }}>
              {summary.inactiveCodes.map((code) => (
                <Tag key={code} style={{ marginBottom: 4 }}>
                  {code}
                </Tag>
              ))}
            </div>
          </div>
        )}
      </Space>
    </Card>
  );
}

function MatrixCell({
  row,
  registerId,
  canManage,
  onEditDefinition,
  onToggleActive,
}: {
  row: PaymentMethodMatrixRow;
  registerId: string;
  canManage: boolean;
  onEditDefinition: (definition: PaymentMethodDefinitionAdmin) => void;
  onToggleActive: (definition: PaymentMethodDefinitionAdmin, nextActive: boolean) => void;
}) {
  const { t } = useI18n();
  const cell = row.byRegister[registerId];

  if (!cell) {
    return (
      <Tooltip title={t('settings.paymentMethods.matrix.notConfigured')}>
        <MinusCircleOutlined style={{ color: '#bfbfbf', fontSize: 18 }} />
      </Tooltip>
    );
  }

  const icon = cell.isActive ? (
    <CheckCircleFilled style={{ color: '#52c41a', fontSize: 18 }} />
  ) : (
    <CloseCircleFilled style={{ color: '#ff4d4f', fontSize: 18 }} />
  );

  const popover = (
    <Space orientation="vertical" size="small" style={{ maxWidth: 260 }}>
      <Typography.Text strong>{cell.name}</Typography.Text>
      <Typography.Text type="secondary">{row.code}</Typography.Text>
      <div>
        <Tag color={cell.isActive ? 'success' : 'default'}>
          {cell.isActive ? t('settings.paymentMethods.matrix.statusOpen') : t('settings.paymentMethods.matrix.statusClosed')}
        </Tag>
        {cell.isDefault && (
          <Tag color="blue" icon={<StarFilled />}>
            {t('settings.paymentMethods.columns.default')}
          </Tag>
        )}
      </div>
      {canManage && (
        <Space>
          <Button
            size="small"
            type={cell.isActive ? 'default' : 'primary'}
            onClick={() => onToggleActive(cell.definition, !cell.isActive)}
          >
            {cell.isActive
              ? t('settings.paymentMethods.matrix.turnOff')
              : t('settings.paymentMethods.matrix.turnOn')}
          </Button>
          <Button size="small" icon={<EditOutlined />} onClick={() => onEditDefinition(cell.definition)}>
            {t('common.buttons.edit')}
          </Button>
        </Space>
      )}
    </Space>
  );

  return (
    <Popover content={popover} trigger="click" placement="top">
      <Button type="text" style={{ height: 'auto', padding: '4px 8px' }} aria-label={`${row.code} ${registerId}`}>
        <Space size={4}>
          {icon}
          {cell.isDefault && <StarFilled style={{ color: '#1677ff', fontSize: 12 }} />}
        </Space>
      </Button>
    </Popover>
  );
}

export function PaymentMethodMatrixOverview({
  registers,
  methodsByRegisterId,
  loading,
  canManage,
  onManageRegister,
  onEditDefinition,
  onToggleActive,
}: PaymentMethodMatrixOverviewProps) {
  const { t } = useI18n();

  const { rows, summaries } = useMemo(
    () => buildPaymentMethodMatrix(registers, methodsByRegisterId),
    [registers, methodsByRegisterId],
  );

  const columns: ColumnsType<PaymentMethodMatrixRow> = useMemo(() => {
    const methodColumn: ColumnsType<PaymentMethodMatrixRow>[0] = {
      title: t('settings.paymentMethods.matrix.methodColumn'),
      key: 'method',
      fixed: 'left',
      width: 200,
      render: (_, row) => (
        <Space orientation="vertical" size={0}>
          <Typography.Text strong>{row.label}</Typography.Text>
          <Typography.Text type="secondary" code style={{ fontSize: 12 }}>
            {row.code}
          </Typography.Text>
        </Space>
      ),
    };

    const registerColumns: ColumnsType<PaymentMethodMatrixRow> = registers.map((register) => ({
      title: (
        <Button type="link" size="small" onClick={() => onManageRegister(register.id)} style={{ padding: 0 }}>
          {register.registerNumber}
        </Button>
      ),
      key: register.id,
      align: 'center' as const,
      width: 120,
      render: (_, row) => (
        <MatrixCell
          row={row}
          registerId={register.id}
          canManage={canManage}
          onEditDefinition={onEditDefinition}
          onToggleActive={onToggleActive}
        />
      ),
    }));

    return [methodColumn, ...registerColumns];
  }, [registers, canManage, onManageRegister, onEditDefinition, onToggleActive, t]);

  if (loading) {
    return (
      <Flex justify="center" style={{ padding: 48 }}>
        <Spin />
      </Flex>
    );
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('settings.paymentMethods.matrix.intro')}
      </Typography.Paragraph>

      <Flex gap={12} wrap="wrap">
        {summaries.map((summary) => (
          <RegisterSummaryCard
            key={summary.registerId}
            summary={summary}
            onClick={() => onManageRegister(summary.registerId)}
          />
        ))}
      </Flex>

      <Space wrap size="middle">
        <Space size={4}>
          <CheckCircleFilled style={{ color: '#52c41a' }} />
          <Typography.Text type="secondary">{t('settings.paymentMethods.matrix.legendOpen')}</Typography.Text>
        </Space>
        <Space size={4}>
          <CloseCircleFilled style={{ color: '#ff4d4f' }} />
          <Typography.Text type="secondary">{t('settings.paymentMethods.matrix.legendClosed')}</Typography.Text>
        </Space>
        <Space size={4}>
          <MinusCircleOutlined style={{ color: '#bfbfbf' }} />
          <Typography.Text type="secondary">{t('settings.paymentMethods.matrix.legendMissing')}</Typography.Text>
        </Space>
        <Space size={4}>
          <StarFilled style={{ color: '#1677ff' }} />
          <Typography.Text type="secondary">{t('settings.paymentMethods.matrix.legendDefault')}</Typography.Text>
        </Space>
      </Space>

      <Table<PaymentMethodMatrixRow>
        rowKey="code"
        size="small"
        pagination={false}
        scroll={{ x: Math.max(640, 200 + registers.length * 120) }}
        dataSource={rows}
        columns={columns}
        locale={{ emptyText: t('settings.paymentMethods.tableEmpty') }}
      />
    </Space>
  );
}
