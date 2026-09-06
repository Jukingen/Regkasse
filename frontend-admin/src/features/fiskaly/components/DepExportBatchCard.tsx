'use client';

import { Alert, Button, Card, Table, Typography } from 'antd';
import { useState, type Key } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import type { Dayjs } from 'dayjs';

import { recordDownloadHistory } from '@/features/download-history/api/downloadHistoryApi';
import {
  clampFiskalyBatchLimits,
  createFiskalyBatchId,
  DEFAULT_FISKALY_BATCH_LIMITS,
  getFiskalyBatchLimits,
  parseFiskalyBatchError,
  postFiskalyBatchDepExport,
  type FiskalyBatchProgressEvent,
} from '@/features/fiskaly/api/fiskalyBatch';
import { FiskalyBatchProgressModal } from '@/features/fiskaly/components/FiskalyBatchProgressModal';
import { useFiskalyOperationStatusLive } from '@/features/fiskaly/hooks/useFiskalyOperationStatusLive';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

type Props = {
  dateRange: [Dayjs | null, Dayjs | null];
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
};

export function DepExportBatchCard({ dateRange, includeSpecialReceipts, includeDailyClosings }: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { hasPermission } = usePermissions();
  const allowed =
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL) &&
    hasPermission(PERMISSIONS.REPORT_EXPORT) &&
    hasPermission(PERMISSIONS.AUDIT_VIEW);
  const { tenants, isLoading } = useTenantList({ enabled: allowed });
  const [selectedKeys, setSelectedKeys] = useState<Key[]>([]);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [progress, setProgress] = useState<FiskalyBatchProgressEvent | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [progressOpen, setProgressOpen] = useState(false);

  const limitsQuery = useQuery({
    queryKey: ['admin', 'fiskaly', 'batch', 'limits'],
    queryFn: ({ signal }) => getFiskalyBatchLimits(signal),
    enabled: allowed,
    staleTime: 60_000,
  });
  const limits = clampFiskalyBatchLimits(limitsQuery.data ?? DEFAULT_FISKALY_BATCH_LIMITS);

  useFiskalyOperationStatusLive({
    enabled: progressOpen,
    onBatchProgress: (evt) => {
      if (!batchId || evt.batchId !== batchId) return;
      setProgress(evt);
    },
  });

  const mutation = useMutation({
    mutationFn: async () => {
      const fromUtc = dateRange[0]?.toISOString();
      const toUtc = dateRange[1]?.toISOString();
      if (!fromUtc || !toUtc) {
        throw new Error(t('tseFiskaly.batch.dateRangeRequired'));
      }
      const id = createFiskalyBatchId();
      setBatchId(id);
      setErrorMessage(null);
      setProgress({
        batchId: id,
        kind: 'dep-export',
        current: 0,
        total: selectedKeys.length,
        successCount: 0,
        failedCount: 0,
        done: false,
      });
      setProgressOpen(true);
      return postFiskalyBatchDepExport({
        batchId: id,
        tenantIds: selectedKeys.map(String),
        fromUtc,
        toUtc,
        includeSpecialReceipts,
        includeDailyClosings,
      });
    },
    onSuccess: async (result) => {
      setProgress((prev) =>
        prev
          ? { ...prev, done: true, successCount: result.successCount, failedCount: result.failedCount, current: prev.total }
          : {
              batchId: batchId ?? '',
              kind: 'dep-export',
              current: 1,
              total: 1,
              successCount: result.successCount,
              failedCount: result.failedCount,
              done: true,
            }
      );
      triggerBlobDownload(result.blob, result.fileName);
      try {
        await recordDownloadHistory({
          fileName: result.fileName,
          fileType: 'zip',
          fileSize: result.blob.size,
          sourceKind: 'dep-export',
        });
      } catch {
        // best-effort
      }
      notify.success(
        t('tseFiskaly.batch.depDownloadSuccess', {
          ok: result.successCount,
          fail: result.failedCount,
        })
      );
      setSelectedKeys([]);
    },
    onError: (err) => {
      const parsed = parseFiskalyBatchError(err);
      const message =
        parsed?.code === 'BATCH_TOO_LARGE'
          ? t('tseFiskaly.batch.tooLarge', { max: parsed.maxItems ?? limits.maxItems })
          : parsed?.message || (err instanceof Error ? err.message : t('tseFiskaly.batch.failed'));
      setErrorMessage(message);
      setProgress((prev) => (prev ? { ...prev, done: true } : { batchId: batchId ?? '', kind: 'dep-export', current: 0, total: 0, successCount: 0, failedCount: 0, done: true }));
      notify.error(message);
    },
  });

  const run = () => {
    if (selectedKeys.length === 0) {
      notify.warning(t('tseFiskaly.batch.noneSelectedTenants'));
      return;
    }
    if (selectedKeys.length > limits.maxItems) {
      notify.error(t('tseFiskaly.batch.tooLarge', { max: limits.maxItems }));
      return;
    }
    if (!dateRange[0] || !dateRange[1]) {
      notify.warning(t('tseFiskaly.batch.dateRangeRequired'));
      return;
    }
    const execute = () => mutation.mutateAsync();
    if (selectedKeys.length >= limits.warnAtItems) {
      modal.confirm({
        title: t('tseFiskaly.batch.largeWarningTitle'),
        content: t('tseFiskaly.batch.largeWarning', { count: selectedKeys.length, max: limits.maxItems }),
        okText: t('tseFiskaly.batch.execute'),
        cancelText: t('tseFiskaly.operations.cancelAction'),
        onOk: () => execute(),
      });
      return;
    }
    void execute();
  };

  if (!allowed) return null;

  return (
    <>
      <Card title={t('tseFiskaly.batch.depTitle')} style={{ marginBottom: 16 }} loading={isLoading}>
        <Typography.Paragraph type="secondary">{t('tseFiskaly.batch.depHint')}</Typography.Paragraph>
        <Alert type="info" showIcon style={{ marginBottom: 12 }} title={t('tseFiskaly.batch.depUsesRange')} />
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12, gap: 12, flexWrap: 'wrap' }}>
          <Typography.Text type="secondary">
            {t('tseFiskaly.batch.selectedTenants', { count: selectedKeys.length })}
          </Typography.Text>
          <Button type="primary" onClick={run} loading={mutation.isPending} disabled={tenants.length === 0}>
            {t('tseFiskaly.batch.executeDep')}
          </Button>
        </div>
        <Table
          size="small"
          rowKey="id"
          pagination={{ pageSize: 10 }}
          dataSource={tenants}
          rowSelection={{
            selectedRowKeys: selectedKeys,
            onChange: setSelectedKeys,
            getCheckboxProps: () => ({ disabled: mutation.isPending }),
          }}
          columns={[
            { title: t('tseFiskaly.history.colTenant'), dataIndex: 'name' },
            { title: 'Slug', dataIndex: 'slug', width: 160 },
          ]}
        />
      </Card>
      <FiskalyBatchProgressModal
        open={progressOpen}
        kind="dep-export"
        progress={progress}
        errorMessage={errorMessage}
        onClose={() => {
          if (!progress?.done && !errorMessage) return;
          setProgressOpen(false);
        }}
      />
    </>
  );
}
