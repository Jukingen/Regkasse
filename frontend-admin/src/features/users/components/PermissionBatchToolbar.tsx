'use client';

import { CheckOutlined, CloseOutlined, UndoOutlined } from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type PermissionBatchToolbarProps = {
  selectedCount: number;
  disabled?: boolean;
  onAllow: () => void;
  onDeny: () => void;
  onResetToRoleDefault: () => void;
  onClearSelection?: () => void;
};

/**
 * Sticky batch bar: counter + allow / deny / reset-to-role-default.
 * When nothing is selected, still renders a compact reset hint for guided tours.
 */
export function PermissionBatchToolbar({
  selectedCount,
  disabled = false,
  onAllow,
  onDeny,
  onResetToRoleDefault,
  onClearSelection,
}: PermissionBatchToolbarProps) {
  const { t } = useI18n();

  if (selectedCount <= 0) {
    return (
      <div
        data-permission-tour="reset"
        style={{
          marginBottom: 8,
          padding: '6px 10px',
          borderRadius: 6,
          background: 'rgba(0,0,0,0.02)',
          border: '1px dashed rgba(0,0,0,0.12)',
        }}
      >
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('users.permissionOnboarding.resetHint')}
        </Typography.Text>
      </div>
    );
  }

  return (
    <div
      role="toolbar"
      aria-label={t('users.permissionsModal.batchToolbarLabel')}
      data-permission-tour="reset"
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: 8,
        marginBottom: 12,
        padding: '8px 12px',
        borderRadius: 8,
        background: '#e6f4ff',
        border: '1px solid #91caff',
      }}
    >
      <Typography.Text strong style={{ marginRight: 4 }}>
        {t('users.permissionsModal.batchSelectedCount', { count: selectedCount })}
      </Typography.Text>
      <Space size={4} wrap>
        <Button
          size="small"
          type="primary"
          icon={<CheckOutlined />}
          disabled={disabled}
          onClick={onAllow}
        >
          {t('users.permissionsModal.batchAllow')}
        </Button>
        <Button
          size="small"
          danger
          icon={<CloseOutlined />}
          disabled={disabled}
          onClick={onDeny}
        >
          {t('users.permissionsModal.batchDeny')}
        </Button>
        <Button
          size="small"
          icon={<UndoOutlined />}
          disabled={disabled}
          onClick={onResetToRoleDefault}
        >
          {t('users.permissionsModal.batchResetRole')}
        </Button>
        {onClearSelection ? (
          <Button size="small" type="link" onClick={onClearSelection}>
            {t('users.permissionsModal.batchClearSelection')}
          </Button>
        ) : null}
      </Space>
    </div>
  );
}
