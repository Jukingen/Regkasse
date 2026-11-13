'use client';

import {
  AuditOutlined,
  CopyOutlined,
  QuestionCircleOutlined,
  ShopOutlined,
} from '@ant-design/icons';
import { Button, Card, Space, Typography } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';

export type PermissionCommonTaskId =
  | 'cashier-new-branch'
  | 'copy-manager'
  | 'prepare-audit';

export type PermissionCommonTasksPanelProps = {
  disabled?: boolean;
  onTask: (taskId: PermissionCommonTaskId) => void;
  onStartTour?: () => void;
};

/**
 * Quick-start actions for new administrators managing permissions.
 */
export function PermissionCommonTasksPanel({
  disabled = false,
  onTask,
  onStartTour,
}: PermissionCommonTasksPanelProps) {
  const { t } = useI18n();

  const tasks: Array<{
    id: PermissionCommonTaskId;
    icon: React.ReactNode;
    labelKey: string;
    descriptionKey: string;
  }> = [
    {
      id: 'cashier-new-branch',
      icon: <ShopOutlined />,
      labelKey: 'users.permissionOnboarding.taskCashierTitle',
      descriptionKey: 'users.permissionOnboarding.taskCashierBody',
    },
    {
      id: 'copy-manager',
      icon: <CopyOutlined />,
      labelKey: 'users.permissionOnboarding.taskCopyManagerTitle',
      descriptionKey: 'users.permissionOnboarding.taskCopyManagerBody',
    },
    {
      id: 'prepare-audit',
      icon: <AuditOutlined />,
      labelKey: 'users.permissionOnboarding.taskAuditTitle',
      descriptionKey: 'users.permissionOnboarding.taskAuditBody',
    },
  ];

  return (
    <Card
      size="small"
      title={t('users.permissionOnboarding.commonTasksTitle')}
      extra={
        onStartTour ? (
          <Button
            type="link"
            size="small"
            icon={<QuestionCircleOutlined />}
            onClick={onStartTour}
          >
            {t('users.permissionOnboarding.guidedTour')}
          </Button>
        ) : null
      }
      style={{ marginBottom: 12 }}
    >
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        {tasks.map((task) => (
          <div
            key={task.id}
            style={{
              display: 'flex',
              gap: 10,
              alignItems: 'flex-start',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Typography.Text strong style={{ display: 'block', fontSize: 13 }}>
                {task.icon} {t(task.labelKey)}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t(task.descriptionKey)}
              </Typography.Text>
            </div>
            <Button
              size="small"
              disabled={disabled}
              onClick={() => onTask(task.id)}
            >
              {t('users.permissionOnboarding.taskApply')}
            </Button>
          </div>
        ))}
      </Space>
    </Card>
  );
}
