'use client';

import React, { useCallback, useMemo } from 'react';
import {
  Alert,
  Button,
  Col,
  Modal,
  Progress,
  Row,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  DownloadOutlined,
  SafetyCertificateOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useI18n } from '@/i18n';
import { useBackupVerificationReport } from '@/features/backup/hooks/useBackupVerificationReport';
import { formatBackupBytes } from '@/features/backup-dr/logic/backupFormat';
import type {
  BackupTableStatistics,
  BackupVerificationReport as BackupVerificationReportData,
} from '@/features/backup/logic/backupVerificationReportApi';
import {
  backupVerificationAlertType,
  getBackupVerificationRowDiff,
  isBackupVerificationRowMismatched,
} from '@/features/backup/logic/backupVerificationReportPresentation';
import { exportBackupVerificationReportPdf } from '@/features/backup/logic/backupVerificationReportPdfExport';

export interface BackupVerificationReportProps {
  backupRunId: string;
  open: boolean;
  onClose: () => void;
}

function StatusIcon({ status }: { status: BackupVerificationReportData['status'] }) {
  if (status === 'Verified') {
    return <CheckCircleOutlined style={{ color: 'var(--ant-color-success)' }} />;
  }
  if (status === 'PartiallyVerified') {
    return <WarningOutlined style={{ color: 'var(--ant-color-warning)' }} />;
  }
  return <CloseCircleOutlined style={{ color: 'var(--ant-color-error)' }} />;
}

export function BackupVerificationReport({
  backupRunId,
  open,
  onClose,
}: BackupVerificationReportProps) {
  const { t, formatLocale } = useI18n();
  const { data: report, isLoading, isError } = useBackupVerificationReport(backupRunId, open);

  const statusLabel = useMemo(() => {
    if (!report) return '';
    if (report.status === 'Verified') return t('backupDr.verificationReport.status.Verified');
    if (report.status === 'PartiallyVerified') {
      return t('backupDr.verificationReport.status.PartiallyVerified');
    }
    return t('backupDr.verificationReport.status.NotVerified');
  }, [report, t]);

  const alertDescription = useMemo(() => {
    if (!report) return '';
    if (report.verificationScore >= 90) return t('backupDr.verificationReport.alertVerified');
    if (report.verificationScore >= 70) return t('backupDr.verificationReport.alertPartial');
    return t('backupDr.verificationReport.alertFailed');
  }, [report, t]);

  const columns: ColumnsType<BackupTableStatistics> = useMemo(
    () => [
      {
        title: t('backupDr.verificationReport.tableName'),
        dataIndex: 'tableName',
        key: 'tableName',
      },
      {
        title: t('backupDr.verificationReport.backupRows'),
        dataIndex: 'rowCount',
        key: 'backupRowCount',
        render: (count: number) => count.toLocaleString(formatLocale),
      },
      {
        title: t('backupDr.verificationReport.sourceRows'),
        key: 'sourceRowCount',
        render: (_: unknown, record: BackupTableStatistics) => {
          const diff = getBackupVerificationRowDiff(report, record);
          if (diff.sourceRowCount == null) return '—';
          return diff.sourceRowCount.toLocaleString(formatLocale);
        },
      },
      {
        title: t('backupDr.verificationReport.diffColumn'),
        key: 'diff',
        render: (_: unknown, record: BackupTableStatistics) => {
          const diff = getBackupVerificationRowDiff(report, record);
          if (diff.missingSource) {
            return <Tag color="error">{t('backupDr.verificationReport.diffMissing')}</Tag>;
          }
          if (diff.diff === 0) {
            return <Tag color="success">{t('backupDr.verificationReport.diffIdentical')}</Tag>;
          }
          return (
            <Tag color="warning">
              {t('backupDr.verificationReport.diffRows', {
                count: String(diff.diff),
                percent: String(diff.diffPercent ?? 0),
              })}
            </Tag>
          );
        },
      },
      {
        title: t('backupDr.verificationReport.verified'),
        key: 'verified',
        render: (_: unknown, record: BackupTableStatistics) =>
          record.isVerified ? (
            <Tag color="success">{t('backupDr.verificationReport.rowVerified')}</Tag>
          ) : (
            <Tag color="warning">{t('backupDr.verificationReport.rowNotVerified')}</Tag>
          ),
      },
    ],
    [formatLocale, report, t],
  );

  const handleExportPdf = useCallback(() => {
    if (!report) return;
    const ok = exportBackupVerificationReportPdf(report, t, formatLocale);
    if (!ok) {
      message.error(t('backupDr.verificationReport.exportPdfBlocked'));
      return;
    }
    message.success(t('backupDr.verificationReport.exportPdfReady'));
  }, [formatLocale, report, t]);

  return (
    <Modal
      title={
        <Space>
          <SafetyCertificateOutlined />
          <span>{t('backupDr.verificationReport.modalTitle')}</span>
        </Space>
      }
      open={open}
      onCancel={onClose}
      width={900}
      destroyOnClose
      footer={[
        <Button key="close" onClick={onClose}>
          {t('backupDr.verificationReport.close')}
        </Button>,
        <Button
          key="export"
          icon={<DownloadOutlined />}
          onClick={handleExportPdf}
          disabled={!report}
        >
          {t('backupDr.verificationReport.exportPdf')}
        </Button>,
      ]}
    >
      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 32 }}>
          <Spin />
        </div>
      ) : isError || !report ? (
        <Alert type="error" showIcon message={t('backupDr.verificationReport.noReport')} />
      ) : (
        <>
          <Row gutter={16} style={{ marginBottom: 24 }}>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('backupDr.verificationReport.score')}
                value={report.verificationScore}
                suffix="%"
                valueStyle={{
                  color:
                    report.verificationScore >= 90
                      ? 'var(--ant-color-success)'
                      : 'var(--ant-color-error)',
                }}
                prefix={<StatusIcon status={report.status} />}
              />
              <Progress percent={report.verificationScore} style={{ marginTop: 8 }} />
            </Col>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('backupDr.verificationReport.backupSizeTitle')}
                value={report.totalSizeFormatted || formatBackupBytes(report.totalSizeBytes)}
              />
            </Col>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('backupDr.verificationReport.artifactsTitle')}
                value={report.artifactCount}
              />
            </Col>
          </Row>

          <Alert
            message={`${t('backupDr.verificationReport.statusLabel')}: ${statusLabel}`}
            description={alertDescription}
            type={backupVerificationAlertType(report.verificationScore)}
            showIcon
            style={{ marginBottom: 16 }}
          />

          {report.logicalDumpAnalysisMessage ? (
            <Alert
              type={report.logicalDumpAnalyzed ? 'success' : 'warning'}
              showIcon
              style={{ marginBottom: 16 }}
              message={report.logicalDumpAnalysisMessage}
            />
          ) : null}

          <Table<BackupTableStatistics>
            dataSource={report.tableStatistics}
            columns={columns}
            rowKey="tableName"
            size="small"
            pagination={{ pageSize: 10 }}
            title={() => t('backupDr.verificationReport.tableComparisonTitle')}
            onRow={(record) => ({
              style: isBackupVerificationRowMismatched(report, record)
                ? { background: 'var(--ant-color-warning-bg)' }
                : undefined,
            })}
          />

          {report.sourceStatistics?.analyzedAtUtc ? (
            <div style={{ marginTop: 12, fontSize: 12, color: 'var(--ant-color-text-secondary)' }}>
              {t('backupDr.verificationReport.sourceAnalyzedAt', {
                time: new Date(report.sourceStatistics.analyzedAtUtc).toLocaleString(formatLocale),
              })}
            </div>
          ) : null}
        </>
      )}
    </Modal>
  );
}
