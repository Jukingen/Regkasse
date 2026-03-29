'use client';

import React from 'react';
import { Button, Card, Space, Typography } from 'antd';
import { CloudUploadOutlined, ExperimentOutlined } from '@ant-design/icons';
export interface ManualActionsPanelProps {
  canManage: boolean;
  /** Orval mutation — geniş imza ile uyumlu. */
  backupTrigger: { isPending: boolean; mutate: (...args: unknown[]) => unknown };
  restoreTrigger: { isPending: boolean; mutate: (...args: unknown[]) => unknown };
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
          onClick={() => restoreTrigger.mutate({ data: {} })}
        >
          {t('backupDr.actions.enqueueRestoreDrill')}
        </Button>
      </Space>
    </Card>
  );
}
