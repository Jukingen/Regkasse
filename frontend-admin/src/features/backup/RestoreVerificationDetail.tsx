'use client';

import { CheckCircleFilled, CloseCircleFilled } from '@ant-design/icons';
import { Alert, Button, Card, Descriptions, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useCallback } from 'react';

import { RestoreVerificationProgress } from '@/features/backup/components/RestoreVerificationProgress';
import {
  useRestoreVerificationReport,
  useRestoreVerificationRun,
} from '@/features/backup/hooks/useRestoreVerificationRuns';
import {
  downloadRestoreVerificationReport,
  type RestoreVerificationCheckDto,
  type RestoreVerificationRowCountDto,
} from '@/features/backup/logic/restoreVerificationApi';
import {
  restoreVerificationCheckGlyph,
  restoreVerificationCheckLabelKey,
  restoreVerificationCheckResultColor,
  restoreVerificationCheckResultLabelKey,
  restoreVerificationStatusColor,
  restoreVerificationStatusLabelKey,
  restoreVerificationTypeLabelKey,
  restoreVerificationVerdictColor,
  restoreVerificationVerdictLabelKey,
  triggerDownloadBlob,
} from '@/features/backup/logic/restoreVerificationPresentation';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';
import { ADMIN_RESTORE_VERIFICATION_PATH } from '@/shared/backupAreaRoutes';

export function RestoreVerificationDetail({ runId }: { runId: string }) {
  const { t } = useI18n();
  const notify = useNotify();
  const runQuery = useRestoreVerificationRun(runId);
  const reportQuery = useRestoreVerificationReport(runId);
  const run = runQuery.data;
  const report = reportQuery.data;
  const verdict = report?.verdict ?? run?.verdict ?? 'pending';
  const checks = report?.checks ?? run?.checks ?? [];
  const rowCounts = report?.rowCounts ?? [];
  const failed = report?.failedCheckIds ?? run?.failedCheckIds ?? [];
  const verifiedAt = report?.verifiedAtUtc ?? run?.completedAt ?? run?.requestedAt;

  const exportReport = useCallback(
    async (format: 'csv' | 'pdf') => {
      try {
        const blob = await downloadRestoreVerificationReport(runId, format);
        triggerDownloadBlob(blob, `restore-verification-${runId}.${format}`);
        notify.successKey('backupDr.restoreVerificationPage.export.ready');
      } catch (err) {
        notify.apiError(err, {
          logContext: 'RestoreVerification.export',
          fallbackKey: 'backupDr.restoreVerificationPage.export.failed',
        });
      }
    },
    [notify, runId]
  );

  const checkColumns: ColumnsType<RestoreVerificationCheckDto> = [
    {
      title: t('backupDr.restoreVerificationPage.detail.check'),
      dataIndex: 'id',
      render: (id: string, row) => (
        <Space>
          <span aria-hidden>{restoreVerificationCheckGlyph(row.result)}</span>
          <span>{t(restoreVerificationCheckLabelKey(id))}</span>
        </Space>
      ),
    },
    {
      title: t('backupDr.restoreVerificationPage.detail.result'),
      dataIndex: 'result',
      width: 140,
      render: (result: RestoreVerificationCheckDto['result']) => (
        <Tag color={restoreVerificationCheckResultColor(result)}>
          {t(restoreVerificationCheckResultLabelKey(result))}
        </Tag>
      ),
    },
    {
      title: t('backupDr.restoreVerificationPage.detail.detail'),
      dataIndex: 'detail',
      render: (detail: string | null | undefined) => detail || '—',
    },
  ];

  const countColumns: ColumnsType<RestoreVerificationRowCountDto> = [
    { title: t('backupDr.restoreVerificationPage.rowCounts.id'), dataIndex: 'id', ellipsis: true },
    { title: t('backupDr.restoreVerificationPage.rowCounts.name'), dataIndex: 'name', ellipsis: true },
    { title: t('backupDr.restoreVerificationPage.rowCounts.category'), dataIndex: 'category', width: 160 },
    { title: t('backupDr.restoreVerificationPage.rowCounts.measured'), dataIndex: 'measured', width: 110 },
    {
      title: t('backupDr.restoreVerificationPage.rowCounts.expected'),
      dataIndex: 'expectedAtLeast',
      width: 110,
    },
    { title: t('backupDr.restoreVerificationPage.rowCounts.status'), dataIndex: 'status', width: 120 },
  ];

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Space>
        <Link href={ADMIN_RESTORE_VERIFICATION_PATH} prefetch={false}>
          {t('backupDr.restoreVerificationPage.backToList')}
        </Link>
      </Space>
      <RestoreVerificationProgress />

      {runQuery.isError ? (
        <Alert type="error" showIcon title={t('backupDr.restoreVerificationPage.loadError')} />
      ) : null}

      <Card loading={runQuery.isLoading}>
        <Space orientation="vertical" size={12} style={{ width: '100%' }}>
          <Space align="center" wrap>
            {verdict === 'passed' ? (
              <CheckCircleFilled style={{ color: '#389e0d', fontSize: 32 }} />
            ) : verdict === 'failed' ? (
              <CloseCircleFilled style={{ color: '#cf1322', fontSize: 32 }} />
            ) : null}
            <Typography.Title level={3} style={{ margin: 0 }}>
              {verdict === 'passed' ? '✅ ' : verdict === 'failed' ? '❌ ' : ''}
              {t(restoreVerificationVerdictLabelKey(verdict))}
            </Typography.Title>
            {run ? (
              <Tag color={restoreVerificationStatusColor(run.status)}>
                {t(restoreVerificationStatusLabelKey(run.status))}
              </Tag>
            ) : null}
          </Space>
          <Typography.Text type="secondary">
            {t('backupDr.restoreVerificationPage.verifiedAt')}: {formatDateTime(verifiedAt)}
          </Typography.Text>
          {failed.length > 0 ? (
            <Alert
              type="error"
              showIcon
              title={t('backupDr.restoreVerificationPage.failedChecks', {
                checks: failed.map((id) => t(restoreVerificationCheckLabelKey(id))).join(', '),
              })}
            />
          ) : null}
          <Space>
            <Button onClick={() => void exportReport('pdf')}>
              {t('backupDr.restoreVerificationPage.export.pdf')}
            </Button>
            <Button onClick={() => void exportReport('csv')}>
              {t('backupDr.restoreVerificationPage.export.csv')}
            </Button>
          </Space>
        </Space>
      </Card>

      {run ? (
        <Card title={t('backupDr.restoreVerificationPage.detail.summary')}>
          <Descriptions column={2} size="small">
            <Descriptions.Item label={t('backupDr.restoreVerificationPage.columns.date')}>
              {formatDateTime(run.requestedAt)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerificationPage.columns.backupId')}>
              {run.sourceBackupRunId ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerificationPage.columns.type')}>
              {t(restoreVerificationTypeLabelKey(run.triggerSource))}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerificationPage.columns.status')}>
              <Tag color={restoreVerificationVerdictColor(verdict)}>
                {t(restoreVerificationVerdictLabelKey(verdict))}
              </Tag>
            </Descriptions.Item>
          </Descriptions>
        </Card>
      ) : null}

      <Card title={t('backupDr.restoreVerificationPage.detail.checksTitle')}>
        <Table<RestoreVerificationCheckDto>
          rowKey="id"
          size="small"
          columns={checkColumns}
          dataSource={checks}
          pagination={false}
        />
      </Card>

      <Card title={t('backupDr.restoreVerificationPage.rowCounts.title')}>
        <Table<RestoreVerificationRowCountDto>
          rowKey="id"
          size="small"
          columns={countColumns}
          dataSource={rowCounts}
          pagination={false}
          locale={{ emptyText: t('backupDr.restoreVerificationPage.rowCounts.empty') }}
        />
      </Card>

      <Card title={t('backupDr.restoreVerificationPage.fiscal.title')}>
        <Typography.Title
          level={4}
          style={{
            margin: 0,
            color:
              report?.fiscalSqlResult === 'RESULT OK'
                ? '#389e0d'
                : report?.fiscalSqlResult?.startsWith('RESULT FAIL')
                  ? '#cf1322'
                  : undefined,
          }}
        >
          {report?.fiscalSqlResult ?? t('backupDr.restoreVerificationPage.fiscal.unavailable')}
        </Typography.Title>
      </Card>
    </Space>
  );
}
