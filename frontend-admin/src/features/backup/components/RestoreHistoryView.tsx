'use client';

/**
 * Super Admin restore request history + RKSV report viewer.
 * Source: GET /api/admin/restore/history and .../request/{id}/report.
 */
import { FileTextOutlined } from '@ant-design/icons';
import { Alert, Button, Descriptions, Modal, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useCallback, useMemo, useState } from 'react';

import { CardSkeleton } from '@/components/Skeleton';
import {
  type RestoreReportResponseDto,
  type RestoreRequestStatusDto,
  getManualRestoreReport,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { manualRestoreStatusTagColor } from '@/features/backup-dr/logic/manualRestorePresentation';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { useRestoreHistory } from '@/features/backup/hooks/useRestoreHistory';
import {
  restoreHistoryDisplayDate,
  restoreHistoryStatusLabelKey,
} from '@/features/backup/logic/restoreHistoryPresentation';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';

export function RestoreHistoryView() {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const { canRestore } = useBackupPermissions();
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const history = useRestoreHistory({
    enabled: canRestore,
    page,
    pageSize,
  });

  const [reportOpen, setReportOpen] = useState(false);
  const [reportLoading, setReportLoading] = useState(false);
  const [report, setReport] = useState<RestoreReportResponseDto | null>(null);

  const formatDt = useCallback((iso: string | null | undefined) => formatDateTime(iso), []);

  const openReport = useCallback(
    async (requestId: string) => {
      setReportOpen(true);
      setReportLoading(true);
      setReport(null);
      try {
        const data = await getManualRestoreReport(requestId);
        setReport(data);
      } catch {
        message.error(t('backupDr.restoreHistory.report.loadFailed'));
        setReportOpen(false);
      } finally {
        setReportLoading(false);
      }
    },
    [message, t]
  );

  const columns: ColumnsType<RestoreRequestStatusDto> = useMemo(
    () => [
      {
        title: t('backupDr.restoreHistory.columns.date'),
        key: 'date',
        render: (_: unknown, row) => formatDt(restoreHistoryDisplayDate(row)),
      },
      {
        title: t('backupDr.restoreHistory.columns.backupRun'),
        dataIndex: 'backupRunId',
        ellipsis: true,
        render: (id: string) => (
          <Typography.Text copyable={{ text: id }} style={{ fontSize: 12 }}>
            {id.slice(0, 8)}…
          </Typography.Text>
        ),
      },
      {
        title: t('backupDr.restoreHistory.columns.targetDatabase'),
        dataIndex: 'targetDatabaseName',
        ellipsis: true,
      },
      {
        title: t('backupDr.restoreHistory.columns.requestedBy'),
        dataIndex: 'requestedByEmail',
        ellipsis: true,
        render: (v: string | null | undefined) => v || '—',
      },
      {
        title: t('backupDr.restoreHistory.columns.status'),
        dataIndex: 'status',
        render: (status: string) => {
          const key = restoreHistoryStatusLabelKey(status);
          const label = t(key) === key ? status : t(key);
          return <Tag color={manualRestoreStatusTagColor(status)}>{label}</Tag>;
        },
      },
      {
        title: t('backupDr.restoreHistory.columns.validationOnly'),
        dataIndex: 'validationOnly',
        width: 110,
        render: (v: boolean) =>
          v ? (
            <Tag color="blue">{t('backupDr.restoreHistory.values.validationOnly')}</Tag>
          ) : (
            <Tag color="error">{t('backupDr.restoreHistory.values.notValidationOnly')}</Tag>
          ),
      },
      {
        title: t('backupDr.restoreHistory.columns.actions'),
        key: 'actions',
        width: 140,
        render: (_: unknown, row) => (
          <Space>
            <Button
              size="small"
              icon={<FileTextOutlined />}
              onClick={() => void openReport(row.requestId)}
            >
              {t('backupDr.restoreHistory.actions.report')}
            </Button>
          </Space>
        ),
      },
    ],
    [formatDt, openReport, t]
  );

  if (!canRestore) {
    return (
      <Alert
        type="warning"
        showIcon
        title={t('backupDr.restoreHistory.forbiddenTitle')}
        description={t('backupDr.restoreHistory.forbiddenDescription')}
      />
    );
  }

  return (
    <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('backupDr.restoreHistory.infoTitle')}
        description={t('backupDr.restoreHistory.infoDescription')}
      />

      {history.isError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('backupDr.restoreHistory.loadFailed')}
        />
      ) : null}

      <Table<RestoreRequestStatusDto>
        rowKey="requestId"
        loading={history.isLoading}
        dataSource={history.items}
        columns={columns}
        pagination={{
          current: page,
          pageSize,
          total: history.totalCount,
          onChange: setPage,
          showSizeChanger: false,
        }}
        locale={{ emptyText: t('backupDr.restoreHistory.empty') }}
      />

      <Modal
        title={t('backupDr.restoreHistory.report.title')}
        open={reportOpen}
        onCancel={() => setReportOpen(false)}
        footer={
          <Button onClick={() => setReportOpen(false)}>
            {t('backupDr.manualRestore.actions.close')}
          </Button>
        }
        width={720}
        destroyOnHidden
      >
        {reportLoading ? <CardSkeleton count={1} /> : null}
        {report ? (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.restoreId')}>
              {report.restoreId}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.tenant')}>
              {report.tenantName ||
                report.tenantId ||
                t('backupDr.manualRestore.fields.sharedDump')}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.restoredAt')}>
              {formatDt(report.restoredAt)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.backupDate')}>
              {formatDt(report.backupDate)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.tables')}>
              {report.tablesRestored != null
                ? report.tablesRestored.toLocaleString(formatLocale)
                : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.records')}>
              {report.recordsRestored != null
                ? report.recordsRestored.toLocaleString(formatLocale)
                : t('backupDr.restoreHistory.report.recordsUnknown')}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.status')}>
              {report.status}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.rksv')}>
              {report.rksvCompliant ? (
                <Tag color="success">{t('backupDr.restoreHistory.report.compliant')}</Tag>
              ) : (
                <Tag color="error">{t('backupDr.restoreHistory.report.notCompliant')}</Tag>
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreHistory.report.targetDatabase')}>
              {report.targetDatabaseName}
            </Descriptions.Item>
            {report.complianceFindings?.length ? (
              <Descriptions.Item label={t('backupDr.restoreHistory.report.findings')}>
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {report.complianceFindings.map((f) => (
                    <li key={f}>
                      <Typography.Text code style={{ fontSize: 12 }}>
                        {f}
                      </Typography.Text>
                    </li>
                  ))}
                </ul>
              </Descriptions.Item>
            ) : null}
          </Descriptions>
        ) : null}
      </Modal>
    </>
  );
}
