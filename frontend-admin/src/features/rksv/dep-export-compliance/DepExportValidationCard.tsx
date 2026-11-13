'use client';

import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons';
import { Badge, Button, Card, Empty, List, Select, Space, Typography } from 'antd';
import React, { useEffect, useMemo, useState } from 'react';

import { DateColumn } from '@/components/DateColumn';
import {
  resolveValidationBadgeStatus,
  useDepExportHistoryValidation,
  useDepExportValidationReport,
  useRunDepExportValidation,
  type DepExportHistoryValidationResultDto,
  type DepExportValidationSummaryItemDto,
} from '@/features/rksv/hooks/useDepExportValidation';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type Props = {
  /** When set, binds the card to this history row (skips recent-export picker). */
  exportId?: string | null;
  /** Show tenant-level recent picker from validation report. Default true when exportId omitted. */
  showExportPicker?: boolean;
  style?: React.CSSProperties;
};

function statusLabel(
  t: (key: string) => string,
  status: string | null | undefined,
  isValid?: boolean | null
): string {
  if (status === 'Passed' || isValid === true) {
    return t('rksvHub.depExportValidation.statusValid');
  }
  if (status === 'Failed' || isValid === false) {
    return t('rksvHub.depExportValidation.statusInvalid');
  }
  if (status === 'Pending') {
    return t('rksvHub.depExportValidation.statusPending');
  }
  if (status === 'Skipped') {
    return t('rksvHub.depExportValidation.statusSkipped');
  }
  return t('rksvHub.depExportValidation.statusUnknown');
}

export function DepExportValidationCard({ exportId, showExportPicker, style }: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const reportQuery = useDepExportValidationReport();
  const runMutation = useRunDepExportValidation();

  const recent = reportQuery.data?.recent ?? [];
  const pickerEnabled = showExportPicker ?? !exportId;

  const [selectedExportId, setSelectedExportId] = useState<string | undefined>(
    exportId ?? undefined
  );

  useEffect(() => {
    if (exportId) {
      setSelectedExportId(exportId);
      return;
    }
    if (!selectedExportId && recent.length > 0) {
      setSelectedExportId(recent[0]?.exportId);
    }
  }, [exportId, recent, selectedExportId]);

  const activeExportId = exportId ?? selectedExportId;
  const validationQuery = useDepExportHistoryValidation(activeExportId);

  const selectedSummary: DepExportValidationSummaryItemDto | undefined = useMemo(
    () => recent.find((row) => row.exportId === activeExportId),
    [recent, activeExportId]
  );

  const result: DepExportHistoryValidationResultDto | undefined = validationQuery.data;
  const validationStatus =
    selectedSummary?.validationStatus ??
    (result
      ? result.isValid
        ? 'Passed'
        : result.checks.length > 0 || result.errorMessage
          ? 'Failed'
          : 'Pending'
      : undefined);

  const badgeStatus = resolveValidationBadgeStatus(validationStatus, result?.isValid);
  const checks = result?.checks ?? [];

  const runValidation = async () => {
    if (!activeExportId) {
      notify.warning('rksvHub.depExportValidation.selectExportWarning');
      return;
    }

    try {
      const next = await runMutation.mutateAsync(activeExportId);
      if (next.isValid) {
        notify.successKey('rksvHub.depExportValidation.validateSuccess');
      } else {
        notify.warning('rksvHub.depExportValidation.validateFailed');
      }
      await reportQuery.refetch();
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DepExportValidation.run',
        fallbackKey: 'rksvHub.depExportValidation.validateError',
      });
    }
  };

  const loading = reportQuery.isLoading || validationQuery.isLoading;
  const busy = runMutation.isPending || validationQuery.isFetching;

  return (
    <Card
      title={
        <Space>
          <SafetyCertificateOutlined />
          <span>{t('rksvHub.depExportValidation.title')}</span>
        </Space>
      }
      loading={loading && !result}
      style={style}
      extra={
        reportQuery.data ? (
          <Typography.Text type="secondary">
            {t('rksvHub.depExportValidation.reportSummary', {
              passed: reportQuery.data.passedCount,
              failed: reportQuery.data.failedCount,
              pending: reportQuery.data.pendingCount,
            })}
          </Typography.Text>
        ) : null
      }
    >
      {pickerEnabled ? (
        <div style={{ marginBottom: 16 }}>
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('rksvHub.depExportValidation.selectExport')}
          </Typography.Text>
          <Select
            style={{ width: '100%', maxWidth: 480 }}
            placeholder={t('rksvHub.depExportValidation.selectExportPlaceholder')}
            value={selectedExportId}
            onChange={setSelectedExportId}
            options={recent.map((row) => ({
              value: row.exportId,
              label: `${row.fileName} · ${row.validationStatus ?? 'Pending'}`,
            }))}
            notFoundContent={
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('rksvHub.depExportValidation.noExports')}
              />
            }
          />
        </div>
      ) : null}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        <Space>
          <Badge status={badgeStatus} />
          <Typography.Text strong>
            {statusLabel(t, validationStatus, result?.isValid)}
          </Typography.Text>
          {result?.validatedAt ? (
            <Typography.Text type="secondary">
              <DateColumn date={result.validatedAt} />
            </Typography.Text>
          ) : null}
        </Space>
        <Button
          type="primary"
          icon={<ReloadOutlined />}
          loading={busy}
          disabled={!activeExportId}
          onClick={() => void runValidation()}
        >
          {t('rksvHub.depExportValidation.runNow')}
        </Button>
      </div>

      <div style={{ marginTop: 16 }}>
        {checks.length === 0 ? (
          <Typography.Text type="secondary">
            {activeExportId
              ? t('rksvHub.depExportValidation.noChecksYet')
              : t('rksvHub.depExportValidation.selectExportHint')}
          </Typography.Text>
        ) : (
          <List
            size="small"
            dataSource={checks}
            renderItem={(check) => (
              <List.Item style={{ paddingBlock: 6 }}>
                <Space align="start" style={{ width: '100%' }}>
                  {check.passed ? (
                    <CheckCircleOutlined style={{ color: '#52c41a', marginTop: 3 }} />
                  ) : (
                    <CloseCircleOutlined style={{ color: '#cf1322', marginTop: 3 }} />
                  )}
                  <div style={{ minWidth: 0 }}>
                    <Typography.Text strong>{check.name}</Typography.Text>
                    {check.details ? (
                      <div>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {check.details}
                        </Typography.Text>
                      </div>
                    ) : null}
                  </div>
                </Space>
              </List.Item>
            )}
          />
        )}
        {result?.errorMessage ? (
          <Typography.Paragraph type="danger" style={{ marginTop: 8, marginBottom: 0 }}>
            {result.errorMessage}
          </Typography.Paragraph>
        ) : null}
      </div>
    </Card>
  );
}
