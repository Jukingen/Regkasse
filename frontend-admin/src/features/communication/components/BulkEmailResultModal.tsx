'use client';

import { List, Modal, Statistic, Typography } from 'antd';
import React from 'react';

import type { BulkEmailResult } from '@/api/generated/model';
import { useI18n } from '@/i18n';

export type BulkEmailResultModalProps = {
  open: boolean;
  result: BulkEmailResult | null;
  onClose: () => void;
};

export function BulkEmailResultModal({ open, result, onClose }: BulkEmailResultModalProps) {
  const { t } = useI18n();

  const sent = result?.totalSent ?? 0;
  const failed = result?.totalFailed ?? 0;
  const attempted = result?.totalAttempted ?? 0;
  const failedEmails = result?.failedEmails ?? [];

  return (
    <Modal
      open={open}
      title={t('communication.bulkEmail.resultTitle')}
      onCancel={onClose}
      onOk={onClose}
      okText={t('communication.bulkEmail.close')}
      cancelButtonProps={{ style: { display: 'none' } }}
      destroyOnHidden
    >
      <Typography.Paragraph>
        {t('communication.bulkEmail.resultSummary', { sent, failed })}
      </Typography.Paragraph>
      <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginBottom: 16 }}>
        <Statistic title={t('communication.bulkEmail.attempted', { count: attempted })} value={attempted} />
        <Statistic title={t('communication.bulkEmail.sent', { count: sent })} value={sent} />
        <Statistic title={t('communication.bulkEmail.failed', { count: failed })} value={failed} />
      </div>
      {failedEmails.length > 0 ? (
        <>
          <Typography.Text strong>{t('communication.bulkEmail.failedEmails')}</Typography.Text>
          <List
            size="small"
            bordered
            style={{ marginTop: 8, maxHeight: 200, overflow: 'auto' }}
            dataSource={failedEmails}
            renderItem={(email) => <List.Item>{email}</List.Item>}
          />
        </>
      ) : null}
    </Modal>
  );
}
