'use client';

import { DownOutlined } from '@ant-design/icons';
import { Button, Dropdown, Space, Typography } from 'antd';
import type { MenuProps } from 'antd';

import type { LicenseSalesBulkActionKind } from '@/features/billing/utils/billingSalesBulk';
import { useI18n } from '@/i18n';

export type BillingSalesBulkBarProps = {
  selectedCount: number;
  disabled?: boolean;
  onAction: (action: LicenseSalesBulkActionKind) => void;
};

export function BillingSalesBulkBar({
  selectedCount,
  disabled,
  onAction,
}: BillingSalesBulkBarProps) {
  const { t } = useI18n();

  if (selectedCount <= 0) {
    return null;
  }

  const items: MenuProps['items'] = [
    {
      key: 'extend30',
      label: t('billing.licenseSales.bulk.actions.extend30'),
      onClick: () => onAction('extend30'),
    },
    {
      key: 'extend90',
      label: t('billing.licenseSales.bulk.actions.extend90'),
      onClick: () => onAction('extend90'),
    },
    {
      key: 'extend365',
      label: t('billing.licenseSales.bulk.actions.extend365'),
      onClick: () => onAction('extend365'),
    },
    { type: 'divider' },
    {
      key: 'revoke',
      danger: true,
      label: t('billing.licenseSales.bulk.actions.revoke'),
      onClick: () => onAction('revoke'),
    },
    { type: 'divider' },
    {
      key: 'exportCsv',
      label: t('billing.licenseSales.bulk.actions.exportCsv'),
      onClick: () => onAction('exportCsv'),
    },
  ];

  return (
    <Space
      wrap
      style={{
        width: '100%',
        padding: '8px 12px',
        marginBottom: 12,
        background: 'var(--ant-color-info-bg, #e6f4ff)',
        border: '1px solid var(--ant-color-info-border, #91caff)',
        borderRadius: 8,
      }}
    >
      <Typography.Text>
        {t('billing.licenseSales.bulk.selectedCount', { count: selectedCount })}
      </Typography.Text>
      <Dropdown menu={{ items }} disabled={disabled} trigger={['click']}>
        <Button disabled={disabled}>
          {t('billing.licenseSales.bulk.actionsLabel')} <DownOutlined />
        </Button>
      </Dropdown>
    </Space>
  );
}
