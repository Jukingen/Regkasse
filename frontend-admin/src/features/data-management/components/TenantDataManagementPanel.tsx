'use client';

import {
  CheckCircleOutlined,
  DeleteOutlined,
  DownloadOutlined,
  InfoCircleOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import type { TenantDataTypeSummary } from '@/features/data-management/api/tenantDataManagement';
import { downloadTenantDataExport } from '@/features/data-management/api/tenantDataManagement';
import { DataDeletionRequestModal } from '@/features/data-management/components/DataDeletionRequestModal';
import { DataRightsRequestPanel } from '@/features/data-management/components/DataRightsRequestPanel';
import {
  useConfirmTenantDataDeletion,
  useExecuteTenantDataPurge,
  useTenantDataManagementSummary,
} from '@/features/data-management/hooks/useTenantDataManagement';
import { buildDataExportFileName } from '@/features/data-management/utils/dataExportFileName';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useDownloadPreview } from '@/hooks/useDownloadPreview';
import { usePermissions } from '@/hooks/usePermissions';
import { useSensitiveExportGate } from '@/hooks/useSensitiveExportGate';
import { formatDate, useI18n } from '@/i18n';
import { DownloadPreviewModal } from '@/components/ui/DownloadPreviewModal';
import { estimateTabularExportBytes } from '@/lib/download/downloadPreview';
import { SENSITIVE_EXPORT_KINDS } from '@/lib/download/sensitiveExportSecurity';
import { triggerBlobDownload } from '@/lib/download/exportDownload';

type Props = { tenantId: string };

function lifecycleColor(state: string): string {
  switch (state) {
    case 'Active':
      return 'success';
    case 'Grace':
      return 'warning';
    case 'Locked':
      return 'orange';
    case 'Archived':
      return 'default';
    case 'ExportRequest':
      return 'processing';
    case 'Deleted':
      return 'error';
    default:
      return 'default';
  }
}

function lifecycleLabel(state: string, t: (key: string) => string): string {
  switch (state) {
    case 'Active':
      return t('dataManagement.states.Active');
    case 'Grace':
      return t('dataManagement.states.Grace');
    case 'Locked':
      return t('dataManagement.states.Locked');
    case 'Archived':
      return t('dataManagement.states.Archived');
    case 'ExportRequest':
      return t('dataManagement.states.ExportRequest');
    case 'Deleted':
      return t('dataManagement.states.Deleted');
    default:
      return state;
  }
}

export function TenantDataManagementPanel({ tenantId }: Props) {
  const { t, formatLocale } = useI18n();
  const { message, modal } = useAntdApp();
  const { isSuperAdmin } = usePermissions();
  const downloadPreview = useDownloadPreview();
  const sensitiveGate = useSensitiveExportGate();
  const summaryQuery = useTenantDataManagementSummary(tenantId);
  const confirmMutation = useConfirmTenantDataDeletion(tenantId);
  const executeMutation = useExecuteTenantDataPurge(tenantId);
  const [deletionWizardOpen, setDeletionWizardOpen] = useState(false);
  const [exportBusy, setExportBusy] = useState(false);

  const columns: ColumnsType<TenantDataTypeSummary> = useMemo(
    () => [
      {
        title: t('dataManagement.colType'),
        dataIndex: 'label',
        key: 'label',
      },
      {
        title: t('dataManagement.colCount'),
        dataIndex: 'rowCount',
        key: 'rowCount',
        width: 120,
      },
      {
        title: t('dataManagement.colRetention'),
        key: 'retention',
        render: (_, row) =>
          row.isRksvRetained ? (
            <Tag color="blue">{t('dataManagement.rksvKept')}</Tag>
          ) : (
            <Tag>{t('dataManagement.purgeable')}</Tag>
          ),
      },
    ],
    [t]
  );

  const summary = summaryQuery.data;
  const retention = summary?.retention;

  const onExport = () => {
    const inventoryRows =
      summary?.dataTypes?.reduce((sum, row) => sum + (row.rowCount ?? 0), 0) ?? 0;
    const fileName = buildDataExportFileName(summary?.tenantSlug ?? null);
    sensitiveGate.run({
      kind: SENSITIVE_EXPORT_KINDS.GdprDataExport,
      isSuperAdmin,
      execute: async (headers) => {
        downloadPreview.requestPreview({
          fileName,
          fileType: 'ZIP',
          sizeBytes: estimateTabularExportBytes(inventoryRows, 'json'),
          isSizeEstimate: true,
          contentSummary: t('common.exportDownload.contentRows', { count: inventoryRows }),
          tenantName: summary?.tenantName ?? summary?.tenantSlug,
          hint: summary?.rksvRetentionNote || t('common.exportDownload.contentGeneric'),
          execute: async () => {
            setExportBusy(true);
            try {
              const blob = await downloadTenantDataExport(tenantId, headers);
              triggerBlobDownload(blob, fileName);
              message.success(t('dataManagement.exportSuccess'));
            } catch {
              message.error(t('dataManagement.exportFailed'));
              throw new Error('export failed');
            } finally {
              setExportBusy(false);
            }
          },
        });
      },
    });
  };

  const onConfirmDeletion = () => {
    const requestId = summary?.latestDeletionRequest?.id;
    if (!requestId) return;
    modal.confirm({
      title: t('dataManagement.confirmTitle'),
      content: t('dataManagement.confirmBody'),
      okText: t('dataManagement.confirmOk'),
      onOk: async () => {
        try {
          await confirmMutation.mutateAsync(requestId);
          message.success(t('dataManagement.confirmSuccess'));
        } catch {
          message.error(t('dataManagement.confirmFailed'));
        }
      },
    });
  };

  const onExecutePurge = () => {
    const requestId = summary?.latestDeletionRequest?.id;
    if (!requestId) return;
    modal.confirm({
      title: t('dataManagement.executeTitle'),
      content: t('dataManagement.executeBody'),
      okText: t('dataManagement.executeOk'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          const result = await executeMutation.mutateAsync(requestId);
          if (result.succeeded) {
            message.success(t('dataManagement.executeSuccess'));
          } else {
            message.error(result.error ?? t('dataManagement.executeFailed'));
          }
        } catch {
          message.error(t('dataManagement.executeFailed'));
        }
      },
    });
  };

  if (summaryQuery.isError) {
    return <Alert type="error" title={t('dataManagement.loadFailed')} />;
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <DataRightsRequestPanel tenantId={tenantId} />

      <Card loading={summaryQuery.isLoading}>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap>
            <Typography.Text strong>{t('dataManagement.lifecycle')}</Typography.Text>
            {summary ? (
              <Tag color={lifecycleColor(summary.lifecycleState)}>
                {lifecycleLabel(summary.lifecycleState, t)}
              </Tag>
            ) : null}
            {summary?.isInGracePeriod ? (
              <Tag color="warning">
                {t('dataManagement.graceRemaining', {
                  days: summary.gracePeriodRemainingDays ?? 0,
                })}
              </Tag>
            ) : null}
            {summary?.isLocked ? <Tag color="orange">{t('dataManagement.lockStatus')}</Tag> : null}
            {summary && summary.daysOverdue > 0 ? (
              <Typography.Text type="secondary">
                {t('dataManagement.daysOverdue', { days: summary.daysOverdue })}
              </Typography.Text>
            ) : null}
          </Space>

          <Alert
            type="info"
            showIcon
            icon={<InfoCircleOutlined />}
            title={t('dataManagement.rksvTitle')}
            description={
              summary?.rksvRetentionNote ??
              t('dataManagement.rksvFallback', {
                years: summary?.rksvRetentionYears ?? 7,
              })
            }
          />

          <Space wrap>
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              loading={exportBusy || sensitiveGate.busy}
              disabled={!summary?.canExport}
              onClick={() => void onExport()}
            >
              {t('dataManagement.exportZip')}
            </Button>
            <Button
              danger
              icon={<DeleteOutlined />}
              disabled={!summary?.canRequestDeletion}
              onClick={() => setDeletionWizardOpen(true)}
            >
              {t('dataManagement.requestDeletion')}
            </Button>
            <Button
              icon={<CheckCircleOutlined />}
              loading={confirmMutation.isPending}
              disabled={!summary?.canConfirmDeletion}
              onClick={onConfirmDeletion}
            >
              {t('dataManagement.confirmDeletion')}
            </Button>
            {isSuperAdmin ? (
              <Button
                danger
                type="primary"
                icon={<ThunderboltOutlined />}
                loading={executeMutation.isPending}
                disabled={!summary?.canExecutePurge}
                onClick={onExecutePurge}
              >
                {t('dataManagement.executePurge')}
              </Button>
            ) : null}
          </Space>

          {summary?.latestDeletionRequest ? (
            <Alert
              type="warning"
              showIcon
              title={t('dataManagement.pendingRequestTitle')}
              description={
                summary.latestDeletionRequest.status === 'confirmed' &&
                summary.latestDeletionRequest.purgeEligibleAtUtc
                  ? t('dataManagement.confirmedRequestBody', {
                      status: summary.latestDeletionRequest.status,
                      eligibleAt: formatDate(
                        summary.latestDeletionRequest.purgeEligibleAtUtc,
                        formatLocale
                      ),
                    })
                  : t('dataManagement.pendingRequestBody', {
                      status: summary.latestDeletionRequest.status,
                    })
              }
            />
          ) : null}
        </Space>
      </Card>

      <DataDeletionRequestModal
        tenantId={tenantId}
        open={deletionWizardOpen}
        onClose={() => setDeletionWizardOpen(false)}
      />

      <Card title={t('dataManagement.retentionCardTitle')} loading={summaryQuery.isLoading}>
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={12} sm={8}>
            <Statistic
              title={t('dataManagement.statPayments')}
              value={retention?.rksvData.paymentDetailsCount ?? 0}
            />
          </Col>
          <Col xs={12} sm={8}>
            <Statistic
              title={t('dataManagement.statProducts')}
              value={retention?.nonRksvData.productsCount ?? 0}
            />
          </Col>
          <Col xs={12} sm={8}>
            <Statistic
              title={t('dataManagement.statCustomers')}
              value={retention?.nonRksvData.customersCount ?? 0}
            />
          </Col>
        </Row>
        <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
          <Descriptions.Item label={t('dataManagement.oldestPayment')}>
            {retention?.rksvData.oldestPaymentDate
              ? formatDate(retention.rksvData.oldestPaymentDate, formatLocale)
              : t('dataManagement.admin.noFiscalData')}
          </Descriptions.Item>
          <Descriptions.Item label={t('dataManagement.retentionUntil')}>
            {retention?.rksvData.retentionUntil
              ? formatDate(retention.rksvData.retentionUntil, formatLocale)
              : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('dataManagement.retentionYears')}>
            {retention?.retentionYears ?? summary?.rksvRetentionYears ?? 7}
          </Descriptions.Item>
          <Descriptions.Item label={t('dataManagement.nonRksvPurge')}>
            {retention?.nonRksvData.canBeDeleted
              ? t('dataManagement.purgeable')
              : t('dataManagement.states.Deleted')}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title={t('dataManagement.dataTypesTitle')} loading={summaryQuery.isLoading}>
        <Table
          rowKey="key"
          size="middle"
          pagination={false}
          columns={columns}
          dataSource={summary?.dataTypes ?? []}
        />
      </Card>
      <DownloadPreviewModal {...downloadPreview.modalProps} />
      {sensitiveGate.modals}
    </Space>
  );
}
