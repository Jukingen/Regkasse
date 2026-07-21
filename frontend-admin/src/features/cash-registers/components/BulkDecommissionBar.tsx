'use client';

import { StopOutlined } from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';

import { useI18n } from '@/i18n';

export type BulkDecommissionBarProps = {
  selectedCount: number;
  onDecommission: () => void;
  disabled?: boolean;
};

export function BulkDecommissionBar({
  selectedCount,
  onDecommission,
  disabled,
}: BulkDecommissionBarProps) {
  const { t } = useI18n();

  if (selectedCount <= 0) {
    return null;
  }

  return (
    <Space
      wrap
      style={{
        width: '100%',
        padding: '8px 12px',
        marginBottom: 12,
        background: 'var(--ant-color-warning-bg, #fffbe6)',
        border: '1px solid var(--ant-color-warning-border, #ffe58f)',
        borderRadius: 8,
      }}
    >
      <Typography.Text>
        {t('cashRegisters.bulk.selectedCount', { count: selectedCount })}
      </Typography.Text>
      <Button danger icon={<StopOutlined />} disabled={disabled} onClick={onDecommission}>
        {t('cashRegisters.bulk.decommissionAction')}
      </Button>
    </Space>
  );
}
