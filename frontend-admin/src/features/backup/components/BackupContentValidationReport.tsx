'use client';

import { Alert, Badge, Space, Table, Typography } from 'antd';
import React from 'react';

import {
  normalizeContentValidationStatus,
  type BackupContentValidationDto,
} from '@/features/backup/logic/backupContentValidationApi';
import { useI18n } from '@/i18n';

export function ContentValidationStatusBadge({
  status,
}: {
  status: string | null | undefined;
}) {
  const { t } = useI18n();
  const normalized = normalizeContentValidationStatus(status);
  if (normalized === 'passed') {
    return <Badge status="success" text={t('backup.contentValidationPassed')} />;
  }
  if (normalized === 'failed') {
    return <Badge status="error" text={t('backup.contentValidationFailed')} />;
  }
  if (normalized === 'partial') {
    return <Badge status="warning" text={t('backup.contentValidationPartial')} />;
  }
  if (normalized === 'unavailable') {
    return <Badge status="default" text={t('backup.contentValidationUnavailable')} />;
  }
  return <Badge status="default" text={t('backup.contentValidationUnknown')} />;
}

/** Shared table + fiscal checks report for content validation results. */
export function BackupContentValidationReport({
  report,
}: {
  report: BackupContentValidationDto;
}) {
  const { t } = useI18n();

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="middle">
      <Space wrap align="center">
        <ContentValidationStatusBadge status={report.overallStatus} />
        <Typography.Text type="secondary">{report.summary}</Typography.Text>
      </Space>

      <Typography.Text strong>{t('backup.contentValidationTables')}</Typography.Text>
      <Table
        size="small"
        pagination={false}
        rowKey={(r) => r.tableKey}
        dataSource={report.tables}
        columns={[
          {
            title: t('backup.contentValidationTable'),
            dataIndex: 'tableName',
            render: (_: unknown, r) => r.tableName ?? r.tableKey,
          },
          { title: t('backup.contentValidationManifest'), dataIndex: 'manifestCount' },
          {
            title: t('backup.contentValidationActual'),
            dataIndex: 'actualCount',
            render: (_: unknown, r) => r.actualCount ?? r.liveCount,
          },
          {
            title: t('backup.contentValidationMatch'),
            dataIndex: 'match',
            render: (v: boolean | undefined) => (v ? '✓' : '—'),
          },
          { title: t('backup.contentValidationRowStatus'), dataIndex: 'status' },
        ]}
      />

      {report.fiscalChecks?.length ? (
        <>
          <Typography.Text strong>{t('backup.contentValidationFiscal')}</Typography.Text>
          <Table
            size="small"
            pagination={false}
            rowKey={(r) => r.checkName}
            dataSource={report.fiscalChecks}
            columns={[
              { title: t('backup.contentValidationCheck'), dataIndex: 'checkName' },
              {
                title: t('backup.contentValidationPassedCol'),
                dataIndex: 'passed',
                render: (v: boolean) => (v ? '✓' : '✗'),
              },
              { title: t('backup.contentValidationDetails'), dataIndex: 'details' },
            ]}
          />
        </>
      ) : null}

      {report.warnings?.length ? (
        <Alert type="warning" showIcon title={report.warnings.join(' · ')} />
      ) : null}
    </Space>
  );
}
