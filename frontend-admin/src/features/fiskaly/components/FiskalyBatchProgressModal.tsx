'use client';

import { Alert, Modal, Progress, Table, Tag, Typography } from 'antd';

import type { FiskalyBatchItemResult, FiskalyBatchProgressEvent } from '@/features/fiskaly/api/fiskalyBatch';
import { useI18n } from '@/i18n';

export type FiskalyBatchProgressKind = 'storno' | 'sonderbelege' | 'dep-export';

type Props = {
  open: boolean;
  kind: FiskalyBatchProgressKind;
  progress: FiskalyBatchProgressEvent | null;
  results?: FiskalyBatchItemResult[];
  errorMessage?: string | null;
  onClose: () => void;
};

export function FiskalyBatchProgressModal({ open, kind, progress, results, errorMessage, onClose }: Props) {
  const { t } = useI18n();
  const done = Boolean(progress?.done) || Boolean(errorMessage);
  const total = progress?.total ?? 0;
  const current = progress?.current ?? 0;
  const percent = total > 0 ? Math.min(100, Math.round((current / total) * 100)) : done ? 100 : 0;
  const failed = progress?.failedCount ?? 0;
  const success = progress?.successCount ?? 0;
  const status = errorMessage ? 'exception' : done ? (failed > 0 ? 'normal' : 'success') : 'active';

  const titleKey =
    kind === 'storno'
      ? 'tseFiskaly.batch.progressStorno'
      : kind === 'dep-export'
        ? 'tseFiskaly.batch.progressDep'
        : 'tseFiskaly.batch.progressSonderbelege';

  return (
    <Modal
      open={open}
      title={t(titleKey)}
      footer={null}
      closable={done}
      mask={{ closable: false }}
      keyboard={done}
      onCancel={onClose}
      destroyOnHidden
      width={640}
    >
      <Typography.Paragraph>
        {done
          ? t('tseFiskaly.batch.progressDone')
          : t('tseFiskaly.batch.progressRunning')}
      </Typography.Paragraph>
      {progress?.currentLabel ? (
        <Typography.Paragraph type="secondary" ellipsis>
          {t('tseFiskaly.batch.currentItem', { label: progress.currentLabel })}
        </Typography.Paragraph>
      ) : null}
      {total > 0 ? (
        <Typography.Paragraph type="secondary">
          {t('tseFiskaly.batch.progressCount', {
            current,
            total,
            ok: success,
            fail: failed,
          })}
        </Typography.Paragraph>
      ) : null}
      <Progress percent={percent} status={status} />
      {done ? (
        <Alert
          style={{ marginTop: 12 }}
          type={errorMessage ? 'error' : failed > 0 ? 'warning' : 'success'}
          showIcon
          title={
            errorMessage
              ? errorMessage
              : t('tseFiskaly.batch.summary', { ok: success, fail: failed, total })
          }
        />
      ) : (
        <Alert type="info" showIcon style={{ marginTop: 12 }} title={t('tseFiskaly.batch.noCancelHint')} />
      )}
      {done && results && results.length > 0 ? (
        <Table
          style={{ marginTop: 16 }}
          size="small"
          rowKey="key"
          pagination={results.length > 8 ? { pageSize: 8 } : false}
          dataSource={results}
          columns={[
            {
              title: t('tseFiskaly.batch.colItem'),
              dataIndex: 'label',
              ellipsis: true,
              render: (label: string | null | undefined, row: FiskalyBatchItemResult) =>
                label || row.key,
            },
            {
              title: t('tseFiskaly.batch.colStatus'),
              dataIndex: 'success',
              width: 120,
              render: (ok: boolean) => (
                <Tag color={ok ? 'success' : 'error'}>
                  {ok ? t('tseFiskaly.batch.statusOk') : t('tseFiskaly.batch.statusFail')}
                </Tag>
              ),
            },
            {
              title: t('tseFiskaly.batch.colError'),
              dataIndex: 'error',
              ellipsis: true,
              render: (error: FiskalyBatchItemResult['error']) =>
                error?.message || error?.code || '—',
            },
          ]}
        />
      ) : null}
    </Modal>
  );
}
