'use client';

import { Modal, Progress, Typography } from 'antd';

import type { BatchDownloadProgress } from '@/features/receipts/utils/batchDownloadReceiptPdfs';
import { useI18n } from '@/i18n/I18nProvider';

type Props = {
  open: boolean;
  progress: BatchDownloadProgress | null;
  onCancel?: () => void;
};

export function BatchDownloadProgressModal({ open, progress, onCancel }: Props) {
  const { t } = useI18n();
  const phase = progress?.phase ?? 'fetch';
  const percent = progress?.percent ?? 0;

  const status =
    phase === 'error' ? 'exception' : phase === 'done' ? 'success' : ('active' as const);

  const title =
    phase === 'pack'
      ? t('receipts.batch.progressPacking')
      : phase === 'done'
        ? t('receipts.batch.progressDone')
        : phase === 'error'
          ? t('receipts.batch.progressError')
          : t('receipts.batch.progressFetching');

  return (
    <Modal
      open={open}
      title={t('receipts.batch.progressTitle')}
      footer={null}
      closable={phase === 'done' || phase === 'error'}
      maskClosable={false}
      keyboard={phase === 'done' || phase === 'error'}
      onCancel={onCancel}
      destroyOnHidden
      width={480}
    >
      <Typography.Paragraph>{title}</Typography.Paragraph>
      {progress?.currentFileName ? (
        <Typography.Paragraph type="secondary" ellipsis>
          {progress.currentFileName}
        </Typography.Paragraph>
      ) : null}
      {progress && progress.total > 0 ? (
        <Typography.Paragraph type="secondary">
          {t('receipts.batch.progressCount', {
            current: progress.current,
            total: progress.total,
            failed: progress.failedCount,
          })}
        </Typography.Paragraph>
      ) : null}
      <Progress percent={percent} status={status} />
      {progress?.message ? (
        <Typography.Paragraph type="danger" style={{ marginTop: 12, marginBottom: 0 }}>
          {progress.message}
        </Typography.Paragraph>
      ) : null}
    </Modal>
  );
}
