'use client';

/**
 * Pre-restore summary + live RKSV compliance check for Super Admin validation restore.
 * Uses verification-report (TOC) and GET .../compliance-check — never hard-codes success.
 * Embedded in RestoreModal (not a standalone Modal).
 */
import { Alert, Card, Descriptions, Spin, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useEffect, useMemo } from 'react';

import { TableSkeleton } from '@/components/Skeleton';
import { useRestoreComplianceCheck } from '@/features/backup/hooks/useRestoreComplianceCheck';
import { useRestorePreview } from '@/features/backup/hooks/useRestorePreview';
import {
  complianceAlertTone,
  complianceCheckLabelKey,
  sortComplianceChecks,
} from '@/features/backup/logic/restoreCompliancePresentation';
import {
  type RestorePreviewChangeRow,
  restorePreviewSizeMib,
} from '@/features/backup/logic/restorePreviewPresentation';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';

export type RestorePreviewBackup = {
  id: string;
  fileName?: string | null;
  /** Operating tenant for same-tenant gate when known. */
  tenantId?: string | null;
  tenantName?: string | null;
  backupDate?: string | null;
};

export type RestorePreviewProps = {
  backup: RestorePreviewBackup;
  /** When false, skip fetching (e.g. parent modal closed). Default true. */
  enabled?: boolean;
  size?: 'default' | 'small';
  /** Notifies parent when live compliance result is known (gates restore CTA). */
  onComplianceChange?: (ok: boolean) => void;
};

export function RestorePreview({
  backup,
  enabled = true,
  size = 'small',
  onComplianceChange,
}: RestorePreviewProps) {
  const { t, formatLocale } = useI18n();
  const fetchEnabled = enabled && Boolean(backup.id);

  const {
    data: preview,
    isLoading,
    isError,
  } = useRestorePreview(backup.id, {
    enabled: fetchEnabled,
  });

  const compliance = useRestoreComplianceCheck(backup.id, {
    enabled: fetchEnabled,
    tenantId: backup.tenantId,
  });

  useEffect(() => {
    if (!onComplianceChange) return;
    if (!fetchEnabled) {
      onComplianceChange(false);
      return;
    }
    if (compliance.isLoading || compliance.isError) {
      onComplianceChange(false);
      return;
    }
    onComplianceChange(compliance.succeeded);
  }, [
    compliance.isError,
    compliance.isLoading,
    compliance.succeeded,
    fetchEnabled,
    onComplianceChange,
  ]);

  const columns: ColumnsType<RestorePreviewChangeRow> = useMemo(
    () => [
      {
        title: t('backupDr.manualRestore.restorePreview.columns.table'),
        dataIndex: 'table',
        key: 'table',
      },
      {
        title: t('backupDr.manualRestore.restorePreview.columns.count'),
        dataIndex: 'count',
        key: 'count',
        render: (count: number) => count.toLocaleString(formatLocale),
      },
      {
        title: t('backupDr.manualRestore.restorePreview.columns.changes'),
        key: 'changes',
        render: (_: unknown, row: RestorePreviewChangeRow) => {
          const label = t(`backupDr.manualRestore.restorePreview.changeKind.${row.changeKind}`);
          if (row.diff != null && row.diff > 0 && row.changeKind === 'mismatch') {
            return t('backupDr.manualRestore.restorePreview.changeKind.mismatchWithDiff', {
              label,
              diff: row.diff.toLocaleString(formatLocale),
            });
          }
          return label;
        },
      },
    ],
    [formatLocale, t]
  );

  const sortedChecks = sortComplianceChecks(compliance.data?.checks);
  const tone = complianceAlertTone({
    isLoading: compliance.isLoading,
    isError: compliance.isError,
    succeeded: compliance.data?.succeeded,
  });

  const complianceStatusTag = (() => {
    if (compliance.isLoading) {
      return <Tag>{t('backupDr.manualRestore.restorePreview.compliance.status.checking')}</Tag>;
    }
    if (compliance.isError) {
      return (
        <Tag color="error">
          {t('backupDr.manualRestore.restorePreview.compliance.status.checkFailed')}
        </Tag>
      );
    }
    if (compliance.succeeded) {
      return (
        <Tag color="success">
          {t('backupDr.manualRestore.restorePreview.compliance.status.compliant')}
        </Tag>
      );
    }
    return (
      <Tag color="error">
        {t('backupDr.manualRestore.restorePreview.compliance.status.notCompliant')}
      </Tag>
    );
  })();

  return (
    <Card size={size} title={t('backupDr.manualRestore.restorePreview.cardTitle')}>
      <Alert
        type={tone}
        showIcon
        style={{ marginBottom: 16 }}
        title={t('backupDr.manualRestore.restorePreview.compliance.alertTitle')}
        description={
          compliance.isLoading ? (
            <Spin size="small" />
          ) : compliance.isError ? (
            t('backupDr.manualRestore.restorePreview.compliance.loadFailed')
          ) : (
            <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>
              {sortedChecks.map((check) => (
                <li key={check.name}>
                  {check.passed ? '✓ ' : '✗ '}
                  {t(complianceCheckLabelKey(check.name))}
                  {check.detail ? (
                    <Typography.Text type="secondary" style={{ marginLeft: 6, fontSize: 12 }}>
                      ({check.detail})
                    </Typography.Text>
                  ) : null}
                </li>
              ))}
              {!sortedChecks.length && compliance.data ? (
                <li>
                  {compliance.data.error ||
                    t('backupDr.manualRestore.restorePreview.compliance.noChecks')}
                </li>
              ) : null}
              <li>{t('backupDr.manualRestore.restorePreview.compliance.validationOnlyNote')}</li>
            </ul>
          )
        }
      />

      <Alert
        type="info"
        showIcon
        title={t('backupDr.manualRestore.restorePreview.alertTitle')}
        description={t('backupDr.manualRestore.restorePreview.alertDescription')}
        style={{ marginBottom: 16 }}
      />

      <Descriptions bordered size="small" column={1} style={{ marginBottom: 16 }}>
        <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.backupRun')}>
          {backup.id}
        </Descriptions.Item>
        {backup.tenantName || backup.tenantId ? (
          <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.tenant')}>
            {backup.tenantName || backup.tenantId}
          </Descriptions.Item>
        ) : (
          <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.tenant')}>
            {t('backupDr.manualRestore.fields.sharedDump')}
          </Descriptions.Item>
        )}
        {backup.backupDate ? (
          <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.backupDate')}>
            {formatDateTime(backup.backupDate)}
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item
          label={t('backupDr.manualRestore.restorePreview.labels.complianceStatus')}
        >
          {complianceStatusTag}
        </Descriptions.Item>
      </Descriptions>

      {isLoading ? <TableSkeleton rows={4} cols={3} loading /> : null}

      {isError ? (
        <Alert
          type="error"
          showIcon
          title={t('backupDr.manualRestore.restorePreview.loadFailed')}
        />
      ) : null}

      {preview ? (
        <>
          {!preview.logicalDumpAnalyzed ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 16 }}
              title={t('backupDr.manualRestore.restorePreview.dumpNotAnalyzed')}
              description={
                preview.analysisMessage ||
                t('backupDr.manualRestore.restorePreview.dumpNotAnalyzedDetail')
              }
            />
          ) : null}

          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.tables')}>
              {t('backupDr.manualRestore.restorePreview.values.tables', {
                count: preview.tables,
              })}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.records')}>
              {t('backupDr.manualRestore.restorePreview.values.records', {
                count: preview.records.toLocaleString(formatLocale),
              })}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.manualRestore.restorePreview.labels.size')}>
              {t('backupDr.manualRestore.restorePreview.values.sizeMib', {
                size: restorePreviewSizeMib(preview.sizeBytes),
              })}
              {preview.sizeFormatted && preview.sizeFormatted !== '—' ? (
                <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                  ({preview.sizeFormatted})
                </Typography.Text>
              ) : null}
            </Descriptions.Item>
          </Descriptions>

          <Typography.Paragraph
            type="secondary"
            style={{ marginTop: 12, marginBottom: 8, fontSize: 12 }}
          >
            {t('backupDr.manualRestore.restorePreview.rowCountDisclaimer')}
          </Typography.Paragraph>

          <Table<RestorePreviewChangeRow>
            rowKey="key"
            dataSource={preview.changes}
            columns={columns}
            pagination={false}
            size="small"
            locale={{ emptyText: t('backupDr.manualRestore.restorePreview.emptyTables') }}
            scroll={preview.changes.length > 8 ? { y: 240 } : undefined}
          />
        </>
      ) : null}
    </Card>
  );
}
