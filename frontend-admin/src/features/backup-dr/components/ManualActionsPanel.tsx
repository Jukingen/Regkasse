'use client';

import React from 'react';
import { Button, Card, Space, Typography } from 'antd';
import { CloudUploadOutlined, ExperimentOutlined } from '@ant-design/icons';
import type { UseMutationResult } from '@tanstack/react-query';

export interface ManualActionsPanelProps {
  canManage: boolean;
  backupTrigger: UseMutationResult<unknown, unknown, { data: Record<string, never> }, unknown>;
  restoreTrigger: UseMutationResult<unknown, unknown, void, unknown>;
  t: (k: string) => string;
}

export function ManualActionsPanel({ canManage, backupTrigger, restoreTrigger, t }: ManualActionsPanelProps) {
  return (
    <Card title={t('backupDr.manual.title')} size="small">
      <Typography.Paragraph type="secondary">{t('backupDr.manual.hint')}</Typography.Paragraph>
      <Space wrap>
        <Button
          type="primary"
          icon={<CloudUploadOutlined />}
          disabled={!canManage}
          loading={backupTrigger.isPending}
          onClick={() => backupTrigger.mutate({ data: {} })}
        >
          {t('backupDr.actions.enqueueBackup')}
        </Button>
        <Button
          icon={<ExperimentOutlined />}
          disabled={!canManage}
          loading={restoreTrigger.isPending}
          onClick={() => restoreTrigger.mutate()}
        >
          {t('backupDr.actions.enqueueRestoreDrill')}
        </Button>
      </Space>
    </Card>
  );
}
