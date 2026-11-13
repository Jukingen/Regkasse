'use client';

import { DownloadOutlined, ReloadOutlined, SafetyOutlined, ToolOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  InputNumber,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import dayjs from 'dayjs';
/**
 * Payload-Hash conflicts — mixed surface: Tabs separate read-only investigation (analyze, export, tables)
 * from the Eingriff (repair dry-run / apply). Permissions unchanged; repair requires elevated rights on the API.
 */
import React, { useCallback, useMemo, useState } from 'react';

import {
  downloadOfflinePayloadHashExportCsv,
  getAdminCashRegisters,
} from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  postApiAdminOfflinePayloadHashAnalyze,
  postApiAdminOfflinePayloadHashRepair,
} from '@/api/generated/admin/admin';
import type {
  OfflinePayloadHashAnalyzeResult,
  OfflinePayloadHashRepairResult,
  PayloadHashConflictGroup,
  PayloadHashRepairableItem,
} from '@/api/generated/model';
import { PageSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

function severityColor(severity: string): string {
  if (severity === 'High') return 'red';
  if (severity === 'Medium') return 'orange';
  return 'default';
}

export default function PayloadHashConflictsPage() {
  const { message, modal } = useAntdApp();

  const { t } = useI18n();
  const { user } = useAuth();
  const canRepair = hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const [maxRows, setMaxRows] = useState(10_000);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
  const [lastRepairResult, setLastRepairResult] = useState<OfflinePayloadHashRepairResult | null>(
    null
  );
  const [activeTab, setActiveTab] = useState('investigation');

  const analyzeParams = useMemo(
    () => ({ maxRows, cashRegisterId: cashRegisterId ?? undefined }),
    [maxRows, cashRegisterId]
  );

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: rksvAdminQueryKeys.offlinePayloadHash.analyze(analyzeParams),
    queryFn: () => postApiAdminOfflinePayloadHashAnalyze(analyzeParams),
    staleTime: 60_000,
  });

  const { data: cashRegisters } = useQuery({
    queryKey: rksvAdminQueryKeys.cashRegisters,
    queryFn: getAdminCashRegisters,
    staleTime: 60_000,
  });

  const downloadCsvMutation = useMutation({
    mutationFn: () => downloadOfflinePayloadHashExportCsv(analyzeParams),
    onSuccess: async (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'offline-payload-hash-analyze.csv';
      a.click();
      URL.revokeObjectURL(url);
      message.success(t('rksvHub.payloadHashConflictsPage.csvDownloaded'));
    },
    onError: (e: unknown) =>
      openApiErrorMessage(message.open, t, e, {
        logContext: 'PayloadHashConflicts.exportCsv',
        fallbackKey: 'rksvHub.payloadHashConflictsPage.exportFailed',
      }),
  });

  const dryRunMutation = useMutation({
    mutationFn: () =>
      postApiAdminOfflinePayloadHashRepair({
        maxRows,
        cashRegisterId: cashRegisterId ?? undefined,
        dryRun: true,
      }),
    onSuccess: (res) => {
      setLastRepairResult(res);
      message.success(t('rksvHub.payloadHashConflictsPage.dryRunDone'));
    },
    onError: (e: Error) =>
      message.error(e?.message ?? t('rksvHub.payloadHashConflictsPage.dryRunFailed')),
  });

  const applyMutation = useMutation({
    mutationFn: () =>
      postApiAdminOfflinePayloadHashRepair({
        maxRows,
        cashRegisterId: cashRegisterId ?? undefined,
        dryRun: false,
      }),
    onSuccess: async (res) => {
      setLastRepairResult(res);
      message.success(
        t('rksvHub.payloadHashConflictsPage.repairApplied', { updated: res.updated ?? 0 })
      );
      await refetch();
    },
    onError: (e: unknown) =>
      openApiErrorMessage(message.open, t, e, {
        logContext: 'PayloadHashConflicts.repairApply',
        fallbackKey: 'rksvHub.payloadHashConflictsPage.repairFailed',
      }),
  });

  const conflictColumns = useMemo(
    () => [
      {
        title: t('rksvHub.payloadHashConflictsPage.colRegister'),
        dataIndex: 'cashRegisterId',
        key: 'cashRegisterId',
        width: 120,
        render: (v: string) => (
          <Typography.Text code copyable>
            {v?.slice(0, 8)}…
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colCanonicalHash'),
        dataIndex: 'canonicalHash',
        key: 'canonicalHash',
        width: 120,
        ellipsis: true,
        render: (v: string) => (
          <Typography.Text code copyable>
            {v?.slice(0, 12)}…
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colSkipReason'),
        dataIndex: 'skipReason',
        key: 'skipReason',
        width: 180,
        render: (v: string) => <Tag>{v ?? '—'}</Tag>,
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colSeverity'),
        dataIndex: 'severitySuggestion',
        key: 'severitySuggestion',
        width: 90,
        render: (v: string) => <Tag color={severityColor(v)}>{v ?? '—'}</Tag>,
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colLatestUtc'),
        dataIndex: 'latestCreatedAtUtc',
        key: 'latestCreatedAtUtc',
        width: 160,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colMismatchRowIds'),
        key: 'mismatchRowIds',
        render: (_: unknown, r: PayloadHashConflictGroup) =>
          r.mismatchRowIds?.length ? (
            <Typography.Text copyable={{ text: r.mismatchRowIds.join('; ') }}>
              {t('rksvHub.payloadHashConflictsPage.idCount', { count: r.mismatchRowIds.length })}
            </Typography.Text>
          ) : (
            '—'
          ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colBlockingRowIds'),
        key: 'occupantRowIds',
        render: (_: unknown, r: PayloadHashConflictGroup) =>
          r.occupantRowIds?.length ? (
            <Typography.Text copyable={{ text: r.occupantRowIds.join('; ') }}>
              {t('rksvHub.payloadHashConflictsPage.idCount', { count: r.occupantRowIds.length })}
            </Typography.Text>
          ) : (
            '—'
          ),
      },
    ],
    [t]
  );

  const repairableColumns = useMemo(
    () => [
      {
        title: t('rksvHub.payloadHashConflictsPage.colRegister'),
        dataIndex: 'cashRegisterId',
        key: 'cashRegisterId',
        width: 120,
        render: (v: string) => (
          <Typography.Text code copyable>
            {v?.slice(0, 8)}…
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colCanonicalHash'),
        dataIndex: 'canonicalHash',
        key: 'canonicalHash',
        width: 120,
        ellipsis: true,
        render: (v: string) => (
          <Typography.Text code copyable>
            {v?.slice(0, 12)}…
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colRowId'),
        dataIndex: 'rowId',
        key: 'rowId',
        width: 120,
        render: (v: string) => (
          <Typography.Text code copyable>
            {v}
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.payloadHashConflictsPage.colCreatedAtUtc'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        width: 160,
        render: dateColumnRender('datetime'),
      },
    ],
    [t]
  );

  const confirmApplyRepair = useCallback(() => {
    modal.confirm({
      title: t('rksvHub.payloadHashConflictsPage.modalApplyTitle'),
      content: t('rksvHub.payloadHashConflictsPage.modalApplyContent'),
      okText: t('rksvHub.payloadHashConflictsPage.modalOk'),
      okButtonProps: { danger: true },
      cancelText: t('rksvHub.payloadHashConflictsPage.modalCancel'),
      onOk: () => applyMutation.mutate(),
    });
  }, [t, applyMutation]);

  const result = data as OfflinePayloadHashAnalyzeResult | undefined;
  const conflictGroups = result?.conflictGroups ?? [];
  const repairableItems = result?.repairableItems ?? [];

  return (
    <>
      <AdminPageHeader
        title={t('rksvHub.payloadHashConflictsPage.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
          { title: t('rksvHub.payloadHashConflictsPage.breadcrumb') },
        ]}
      />

      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('rksvHub.payloadHashConflictsPage.intro')}
      </Typography.Paragraph>

      {error && (
        <Alert
          type="error"
          title={t('rksvHub.payloadHashConflictsPage.analyzeFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="PayloadHashConflicts.analyze"
              fallbackKey="rksvHub.payloadHashConflictsPage.unknownError"
            />
          }
          style={{ marginBottom: 16 }}
          showIcon
        />
      )}

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <Space>
            <Typography.Text strong>
              {t('rksvHub.payloadHashConflictsPage.maxRowsLabel')}
            </Typography.Text>
            <InputNumber
              min={1}
              max={100_000}
              value={maxRows}
              onChange={(v) => setMaxRows(v ?? 10_000)}
            />
          </Space>
          <Space>
            <Typography.Text strong>
              {t('rksvHub.payloadHashConflictsPage.registerLabel')}
            </Typography.Text>
            <Select
              placeholder={t('rksvHub.payloadHashConflictsPage.registerPlaceholderAll')}
              allowClear
              value={cashRegisterId ?? undefined}
              onChange={(v) => setCashRegisterId(v ?? undefined)}
              style={{ minWidth: 220 }}
              options={(cashRegisters ?? [])
                .filter((r) => typeof r.id === 'string' && r.id.length > 0)
                .map((r) => ({
                  value: r.id as string,
                  label: r.registerNumber
                    ? `${r.registerNumber} (${(r.id as string).slice(0, 8)}…)`
                    : (r.id as string),
                }))}
            />
          </Space>
          <Button type="primary" onClick={() => refetch()}>
            {t('rksvHub.payloadHashConflictsPage.runAnalyze')}
          </Button>
        </Space>
      </Card>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'investigation',
            label: t('rksvHub.payloadHashConflictsPage.tabInvestigation'),
            children: (
              <>
                <Space wrap style={{ marginBottom: 16 }}>
                  <Button icon={<ReloadOutlined />} onClick={() => refetch()}>
                    {t('rksvHub.payloadHashConflictsPage.refreshAnalysis')}
                  </Button>
                  <Button
                    icon={<DownloadOutlined />}
                    loading={downloadCsvMutation.isPending}
                    onClick={() => downloadCsvMutation.mutate()}
                  >
                    {t('rksvHub.payloadHashConflictsPage.exportCsv')}
                  </Button>
                </Space>
                {isLoading && !result ? (
                  <PageSkeleton widgets={4} />
                ) : result ? (
                  <>
                    <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                      <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                          <Statistic
                            title={t('rksvHub.payloadHashConflictsPage.statScanned')}
                            value={result.scanned}
                          />
                        </Card>
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                          <Statistic
                            title={t('rksvHub.payloadHashConflictsPage.statMismatch')}
                            value={result.runtimeMismatchCount}
                          />
                        </Card>
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                          <Statistic
                            title={t('rksvHub.payloadHashConflictsPage.statRepairableNoConflict')}
                            value={result.repairableNoConflictCount}
                          />
                        </Card>
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                          <Statistic
                            title={t('rksvHub.payloadHashConflictsPage.statConflictSkipped')}
                            value={result.skippedWouldConflictCount}
                          />
                        </Card>
                      </Col>
                    </Row>

                    {result.legacyDataQualityRiskHigh && result.warningMessage && (
                      <Alert
                        type="warning"
                        title={t('rksvHub.payloadHashConflictsPage.legacyDataQualityTitle')}
                        description={result.warningMessage}
                        style={{ marginBottom: 16 }}
                        showIcon
                      />
                    )}

                    <Card
                      size="small"
                      title={t('rksvHub.payloadHashConflictsPage.conflictGroupsTitle', {
                        count: conflictGroups.length,
                      })}
                      style={{ marginBottom: 16 }}
                    >
                      {conflictGroups.length === 0 ? (
                        <Typography.Text type="secondary">
                          {t('rksvHub.payloadHashConflictsPage.noConflictsInScope')}
                        </Typography.Text>
                      ) : (
                        <Table
                          columns={conflictColumns}
                          dataSource={conflictGroups}
                          rowKey={(r) => `${r.cashRegisterId}-${r.canonicalHash}-${r.skipReason}`}
                          pagination={{
                            pageSize: 20,
                            showTotal: (tot) =>
                              t('rksvHub.payloadHashConflictsPage.paginationTotal', { total: tot }),
                          }}
                          size="small"
                          scroll={{ x: 900 }}
                        />
                      )}
                    </Card>

                    <Card
                      size="small"
                      title={t('rksvHub.payloadHashConflictsPage.repairableTitle', {
                        count: repairableItems.length,
                      })}
                    >
                      {repairableItems.length === 0 ? (
                        <Typography.Text type="secondary">
                          {t('rksvHub.payloadHashConflictsPage.noRepairableInScope')}
                        </Typography.Text>
                      ) : (
                        <Table
                          columns={repairableColumns}
                          dataSource={repairableItems}
                          rowKey="rowId"
                          pagination={{
                            pageSize: 20,
                            showTotal: (tot) =>
                              t('rksvHub.payloadHashConflictsPage.paginationTotal', { total: tot }),
                          }}
                          size="small"
                          scroll={{ x: 600 }}
                        />
                      )}
                    </Card>

                    <Alert
                      type="info"
                      title={t('rksvHub.payloadHashConflictsPage.investigationReadOnlyTitle')}
                      description={t('rksvHub.payloadHashConflictsPage.investigationReadOnlyBody')}
                      style={{ marginTop: 16 }}
                      showIcon
                    />
                  </>
                ) : (
                  <Typography.Text type="secondary">
                    {t('rksvHub.payloadHashConflictsPage.runAnalysisHint')}
                  </Typography.Text>
                )}
              </>
            ),
          },
          {
            key: 'repair',
            label: t('rksvHub.payloadHashConflictsPage.tabRepair'),
            children: (
              <>
                <Alert
                  type="error"
                  showIcon
                  title={t('rksvHub.payloadHashConflictsPage.repairDangerTitle')}
                  description={t('rksvHub.payloadHashConflictsPage.repairDangerBody')}
                  style={{ marginBottom: 16 }}
                />
                {!canRepair && (
                  <Alert
                    type="warning"
                    title={t('rksvHub.payloadHashConflictsPage.repairLockedTitle')}
                    description={t('rksvHub.payloadHashConflictsPage.repairLockedBody')}
                    style={{ marginBottom: 16 }}
                    showIcon
                  />
                )}
                <Space wrap style={{ marginBottom: 16 }}>
                  <Button
                    icon={<ToolOutlined />}
                    onClick={() => dryRunMutation.mutate()}
                    loading={dryRunMutation.isPending}
                    disabled={!canRepair || applyMutation.isPending}
                  >
                    {t('rksvHub.payloadHashConflictsPage.dryRunButton')}
                  </Button>
                  <Button
                    danger
                    icon={<SafetyOutlined />}
                    loading={applyMutation.isPending}
                    disabled={!canRepair || dryRunMutation.isPending}
                    onClick={confirmApplyRepair}
                  >
                    {t('rksvHub.payloadHashConflictsPage.applyRepairButton')}
                  </Button>
                </Space>
                {lastRepairResult && (
                  <Card
                    size="small"
                    title={
                      lastRepairResult.dryRun
                        ? t('rksvHub.payloadHashConflictsPage.resultTitleDryRun')
                        : t('rksvHub.payloadHashConflictsPage.resultTitleApply')
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Row gutter={[16, 16]}>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultScanned')}
                          value={lastRepairResult.scanned}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultUpdated')}
                          value={lastRepairResult.updated}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultSkippedConflict')}
                          value={lastRepairResult.skippedConflict}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultSkippedAligned')}
                          value={lastRepairResult.skippedAlreadyAligned}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultSkippedNullPayload')}
                          value={lastRepairResult.skippedNullPayload}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={4}>
                        <Statistic
                          title={t('rksvHub.payloadHashConflictsPage.resultSkippedNormalizeError')}
                          value={lastRepairResult.skippedNormalizeError}
                        />
                      </Col>
                    </Row>
                  </Card>
                )}
              </>
            ),
          },
        ]}
      />
    </>
  );
}
