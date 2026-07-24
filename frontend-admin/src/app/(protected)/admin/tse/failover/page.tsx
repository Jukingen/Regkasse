'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  List,
  Modal,
  Progress,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Timeline,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getFailoverHistory,
  getFailoverStatus,
  getTseComplianceReport,
  getTseComplianceStatus,
  getTseCostAnomalies,
  getTseCostReport,
  getTseDevices,
  getTseFailurePrediction,
  getTseHealthForecast,
  getTseHealthReport,
  getTseHealthTrend,
  getTsePerformanceAnomalies,
  getTsePerformanceMetrics,
  manualFailover,
  revertFailover,
} from '@/features/tse-failover/api/tse';
import { TseHealthTrendChart } from '@/features/tse-failover/components/TseHealthTrendChart';
import { TsePerformanceChart } from '@/features/tse-failover/components/TsePerformanceChart';
import type { TseFailoverDevice } from '@/features/tse-failover/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDate, formatUtcDateTime } from '@/lib/dateUtils';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const DEVICES_KEY = ['admin', 'tse-failover', 'devices'] as const;
const STATUS_KEY = ['admin', 'tse-failover', 'status'] as const;
const HISTORY_KEY = ['admin', 'tse-failover', 'history'] as const;

function healthTagColor(status: string): string {
  switch (status) {
    case 'Healthy':
      return 'success';
    case 'Degraded':
      return 'warning';
    case 'Unhealthy':
    case 'Offline':
    case 'Expired':
    case 'Revoked':
      return 'error';
    default:
      return 'default';
  }
}

export default function TseFailoverPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();

  const [manualPrimaryId, setManualPrimaryId] = useState<string | null>(null);
  const [manualBackupId, setManualBackupId] = useState<string | undefined>();
  const [reportTenantId, setReportTenantId] = useState<string | undefined>();
  const [trendDays, setTrendDays] = useState(7);
  const [perfDeviceId, setPerfDeviceId] = useState<string | undefined>();

  const devicesQuery = useQuery({
    queryKey: DEVICES_KEY,
    queryFn: ({ signal }) => getTseDevices(signal),
    refetchInterval: 30_000,
    enabled: allowed,
  });

  const statusQuery = useQuery({
    queryKey: STATUS_KEY,
    queryFn: ({ signal }) => getFailoverStatus(signal),
    refetchInterval: 15_000,
    enabled: allowed,
  });

  const historyQuery = useQuery({
    queryKey: HISTORY_KEY,
    queryFn: ({ signal }) => getFailoverHistory(50, signal),
    enabled: allowed,
  });

  const healthReportQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'health-report', reportTenantId],
    queryFn: ({ signal }) => getTseHealthReport(reportTenantId!, signal),
    enabled: allowed && !!reportTenantId,
  });

  const healthTrendQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'health-trend', reportTenantId, trendDays],
    queryFn: ({ signal }) => getTseHealthTrend(reportTenantId!, trendDays, undefined, signal),
    enabled: allowed && !!reportTenantId,
  });

  const tenantDevices = useMemo(() => {
    const all = devicesQuery.data ?? [];
    if (!reportTenantId) return [];
    return all.filter((d) => d.tenantId === reportTenantId);
  }, [devicesQuery.data, reportTenantId]);

  const effectivePerfDeviceId = useMemo(() => {
    if (perfDeviceId && tenantDevices.some((d) => d.id === perfDeviceId)) {
      return perfDeviceId;
    }
    const primary = tenantDevices.find((d) => d.isPrimary);
    return primary?.id ?? tenantDevices[0]?.id;
  }, [perfDeviceId, tenantDevices]);

  const performanceQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'performance', effectivePerfDeviceId, trendDays],
    queryFn: ({ signal }) => getTsePerformanceMetrics(effectivePerfDeviceId!, trendDays, signal),
    enabled: allowed && !!effectivePerfDeviceId,
  });

  const performanceAnomalyQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'performance-anomalies', effectivePerfDeviceId],
    queryFn: ({ signal }) => getTsePerformanceAnomalies(effectivePerfDeviceId!, signal),
    enabled: allowed && !!effectivePerfDeviceId,
    refetchInterval: 60_000,
  });

  const complianceStatusQuery = useQuery({
    queryKey: ['admin', 'tse-compliance', 'status', reportTenantId],
    queryFn: ({ signal }) => getTseComplianceStatus(reportTenantId!, signal),
    enabled: allowed && !!reportTenantId,
    refetchInterval: 60_000,
  });

  const complianceReportQuery = useQuery({
    queryKey: ['admin', 'tse-compliance', 'report', reportTenantId, trendDays],
    queryFn: ({ signal }) => {
      const toUtc = new Date();
      const fromUtc = new Date(toUtc.getTime() - trendDays * 24 * 60 * 60 * 1000);
      return getTseComplianceReport(
        reportTenantId!,
        fromUtc.toISOString(),
        toUtc.toISOString(),
        signal
      );
    },
    enabled: allowed && !!reportTenantId,
  });

  const costReportQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'cost-report', reportTenantId],
    queryFn: ({ signal }) => getTseCostReport(reportTenantId!, 30, signal),
    enabled: allowed && !!reportTenantId,
  });

  const costAnomalyQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'cost-anomalies', reportTenantId],
    queryFn: ({ signal }) => getTseCostAnomalies(reportTenantId!, signal),
    enabled: allowed && !!reportTenantId,
    refetchInterval: 60_000,
  });

  const predictionQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'predict-failure', effectivePerfDeviceId],
    queryFn: ({ signal }) => getTseFailurePrediction(effectivePerfDeviceId!, signal),
    enabled: allowed && !!effectivePerfDeviceId,
    refetchInterval: 60_000,
  });

  const healthForecastQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'health-forecast', effectivePerfDeviceId, trendDays],
    queryFn: ({ signal }) => getTseHealthForecast(effectivePerfDeviceId!, trendDays, signal),
    enabled: allowed && !!effectivePerfDeviceId,
  });

  const invalidateAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: DEVICES_KEY }),
      queryClient.invalidateQueries({ queryKey: STATUS_KEY }),
      queryClient.invalidateQueries({ queryKey: HISTORY_KEY }),
    ]);
  };

  const manualMutation = useMutation({
    mutationFn: (body: { primaryDeviceId: string; backupDeviceId: string }) => manualFailover(body),
    onSuccess: async () => {
      notify.success(t('tseFailover.manualSuccess'));
      setManualPrimaryId(null);
      setManualBackupId(undefined);
      await invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseFailover.manual',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const revertMutation = useMutation({
    mutationFn: (primaryDeviceId: string) => revertFailover(primaryDeviceId),
    onSuccess: async () => {
      notify.success(t('tseFailover.revertSuccess'));
      await invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseFailover.revert',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const devices = devicesQuery.data ?? [];
  const status = statusQuery.data;
  const history = historyQuery.data ?? [];
  const report = healthReportQuery.data;

  const tenantOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const d of devices) {
      if (!d.tenantId) continue;
      map.set(d.tenantId, d.tenantName || d.tenantSlug || d.tenantId);
    }
    return Array.from(map.entries()).map(([value, label]) => ({ value, label }));
  }, [devices]);

  const backupOptions = useMemo(() => {
    if (!manualPrimaryId) return [];
    return devices
      .filter(
        (d) =>
          d.isBackup &&
          d.isActive &&
          (d.primaryDeviceId === manualPrimaryId || !d.primaryDeviceId)
      )
      .map((d) => ({
        value: d.id,
        label: `${d.serialNumber}${d.deviceId ? ` (${d.deviceId})` : ''} — ${d.healthStatus}`,
      }));
  }, [devices, manualPrimaryId]);

  const columns: ColumnsType<TseFailoverDevice> = [
    {
      title: t('tseFailover.colDevice'),
      key: 'device',
      render: (_, row) => (
        <Space direction="vertical" size={0}>
          <Typography.Text code style={{ fontSize: 12 }}>
            {row.deviceId || row.serialNumber}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.serialNumber}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: t('tseFailover.colTenant'),
      key: 'tenant',
      render: (_, row) => row.tenantName || row.tenantSlug || t('tseFailover.unknownTenant'),
    },
    {
      title: t('tseFailover.colType'),
      key: 'type',
      render: (_, row) => (
        <Space wrap>
          <Tag color={row.isPrimary ? 'blue' : 'orange'}>
            {row.isPrimary ? t('tseFailover.typePrimary') : t('tseFailover.typeBackup')}
          </Tag>
          {row.isFailoverActive ? (
            <Tag color="red">{t('tseFailover.tagActiveFailover')}</Tag>
          ) : null}
        </Space>
      ),
    },
    {
      title: t('tseFailover.colStatus'),
      dataIndex: 'healthStatus',
      key: 'healthStatus',
      render: (statusValue: string) => (
        <Tag color={healthTagColor(statusValue)}>
          {t(`tseFailover.health.${statusValue}` as 'tseFailover.health.Healthy')}
        </Tag>
      ),
    },
    {
      title: t('tseFailover.colHealthScore'),
      dataIndex: 'healthScore',
      key: 'healthScore',
      width: 140,
      render: (score: number) => (
        <Progress
          percent={score ?? 0}
          size="small"
          status={(score ?? 0) >= 80 ? 'success' : (score ?? 0) >= 50 ? 'normal' : 'exception'}
        />
      ),
    },
    {
      title: t('tseFailover.colFailoverCount'),
      dataIndex: 'failoverCount',
      key: 'failoverCount',
      width: 110,
    },
    {
      title: t('tseFailover.colActions'),
      key: 'actions',
      render: (_, row) => (
        <Space wrap>
          {row.isPrimary && row.isActive ? (
            <Button
              size="small"
              onClick={() => {
                setManualPrimaryId(row.id);
                setManualBackupId(undefined);
              }}
            >
              {t('tseFailover.actionForceFailover')}
            </Button>
          ) : null}
          {row.isFailoverActive && row.primaryDeviceId ? (
            <Button
              size="small"
              type="primary"
              loading={revertMutation.isPending}
              onClick={() => revertMutation.mutate(row.primaryDeviceId!)}
            >
              {t('tseFailover.actionRevert')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  if (!allowed) {
    return (
      <div style={{ padding: 24 }}>
        <Alert type="error" showIcon message={t('tseFailover.forbidden')} />
      </div>
    );
  }

  return (
    <div style={{ padding: 24 }}>
      <AdminPageHeader
        title={t('tseFailover.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.adminTseManagement'), href: '/admin/tse-management' },
          { title: t('tseFailover.title') },
        ]}
      >
        <Typography.Text type="secondary">{t('tseFailover.subtitle')}</Typography.Text>
      </AdminPageHeader>

      {status && !status.autoFailoverEnabled ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={t('tseFailover.autoFailoverOff')}
        />
      ) : null}

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('tseFailover.statActive')} value={status?.activeDeviceCount ?? 0} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseFailover.statHealthy')}
              value={status?.healthyDeviceCount ?? 0}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseFailover.statFailoverActive')}
              value={status?.activeFailoverCount ?? 0}
              valueStyle={{ color: '#faad14' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseFailover.statBackupAvailable')}
              value={status?.backupAvailableCount ?? 0}
            />
          </Card>
        </Col>
      </Row>

      {(status?.activeFailovers?.length ?? 0) > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={t('tseFailover.activeAlertTitle')}
          description={
            <Space direction="vertical" style={{ width: '100%' }}>
              {status!.activeFailovers.map((f) => (
                <Space key={f.id} wrap style={{ width: '100%', justifyContent: 'space-between' }}>
                  <Typography.Text>
                    {f.tenantName ? `${f.tenantName}: ` : ''}
                    {t('tseFailover.activeAlertPrimary')}:{' '}
                    <code>{f.primarySerialNumber || f.primaryDeviceId}</code>
                    {' → '}
                    {t('tseFailover.activeAlertBackup')}:{' '}
                    <code>{f.backupSerialNumber || f.backupDeviceId}</code>
                  </Typography.Text>
                  <Button
                    size="small"
                    loading={revertMutation.isPending}
                    onClick={() => revertMutation.mutate(f.primaryDeviceId)}
                  >
                    {t('tseFailover.actionRevert')}
                  </Button>
                </Space>
              ))}
            </Space>
          }
        />
      ) : null}

      <Card
        title={t('tseFailover.healthReportTitle')}
        style={{ marginBottom: 16 }}
        extra={
          <Space wrap>
            <Select
              style={{ minWidth: 220 }}
              placeholder={t('tseFailover.healthReportSelectTenant')}
              options={tenantOptions}
              value={reportTenantId}
              onChange={(v) => {
                setReportTenantId(v);
                setPerfDeviceId(undefined);
              }}
              allowClear
              showSearch
              optionFilterProp="label"
            />
            <Select
              style={{ width: 120 }}
              value={trendDays}
              onChange={setTrendDays}
              options={[
                { value: 7, label: `7 ${t('tseFailover.trendDaysLabel')}` },
                { value: 14, label: `14 ${t('tseFailover.trendDaysLabel')}` },
                { value: 30, label: `30 ${t('tseFailover.trendDaysLabel')}` },
              ]}
            />
          </Space>
        }
      >
        {!reportTenantId ? (
          <Typography.Text type="secondary">{t('tseFailover.healthReportSelectTenant')}</Typography.Text>
        ) : (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Row gutter={[16, 16]}>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('tseFailover.statHealthy')} value={report?.healthyDevices ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('tseFailover.statDegraded')} value={report?.degradedDevices ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('tseFailover.statUnhealthy')} value={report?.unhealthyDevices ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic
                  title={t('tseFailover.statAvgScore')}
                  value={report?.averageHealthScore ?? 0}
                  precision={1}
                />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title="Min" value={report?.minHealthScore ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title="Max" value={report?.maxHealthScore ?? 0} />
              </Col>
            </Row>

            <Card size="small" title={t('tseFailover.trendTitle')} type="inner">
              <TseHealthTrendChart
                data={healthTrendQuery.data}
                loading={healthTrendQuery.isLoading}
                healthyMinScore={report?.healthyMinScore ?? 80}
                degradedMinScore={report?.degradedMinScore ?? 50}
              />
            </Card>

            <Card size="small" type="inner" title={t('tseFailover.complianceTitle')}>
              {(() => {
                const status = complianceStatusQuery.data?.status;
                const statusColor =
                  status === 'NonCompliant' ? 'error' : status === 'AtRisk' ? 'warning' : 'success';
                const statusLabel =
                  status === 'NonCompliant'
                    ? t('tseFailover.complianceNonCompliant')
                    : status === 'AtRisk'
                      ? t('tseFailover.complianceAtRisk')
                      : t('tseFailover.complianceFullyOk');
                const compliance = complianceReportQuery.data;
                return (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Space wrap>
                      <Tag color={statusColor}>
                        {t('tseFailover.complianceStatusLabel')}: {statusLabel}
                      </Tag>
                      <Typography.Text type="secondary">
                        {t('tseFailover.complianceLegalNote')}
                      </Typography.Text>
                    </Space>

                    <Row gutter={[16, 16]}>
                      <Col xs={12} sm={8} md={4}>
                        <Statistic
                          title={t('tseFailover.complianceSigned')}
                          value={compliance?.signedTransactions ?? 0}
                        />
                      </Col>
                      <Col xs={12} sm={8} md={4}>
                        <Statistic
                          title={t('tseFailover.complianceUnsigned')}
                          value={
                            compliance?.unsignedTransactions ??
                            complianceStatusQuery.data?.unsignedTransactions ??
                            0
                          }
                        />
                      </Col>
                      <Col xs={12} sm={8} md={4}>
                        <Statistic
                          title={t('tseFailover.complianceChainBreaks')}
                          value={
                            compliance?.signatureChainSummary?.chainBreakCount ??
                            complianceStatusQuery.data?.chainBreakCount ??
                            0
                          }
                        />
                      </Col>
                      <Col xs={12} sm={8} md={4}>
                        <Statistic
                          title={t('tseFailover.statUnhealthy')}
                          value={
                            compliance?.healthSummary?.unhealthyDevices ??
                            complianceStatusQuery.data?.unhealthyDevices ??
                            0
                          }
                        />
                      </Col>
                    </Row>

                    <Row gutter={[16, 16]}>
                      <Col xs={24} lg={12}>
                        <Typography.Text strong>{t('tseFailover.complianceIssues')}</Typography.Text>
                        {(compliance?.issues?.length ?? 0) === 0 ? (
                          <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                            {t('tseFailover.complianceEmptyIssues')}
                          </Typography.Paragraph>
                        ) : (
                          <List
                            size="small"
                            dataSource={compliance?.issues ?? []}
                            renderItem={(item) => (
                              <List.Item>
                                <Space align="start">
                                  <Tag
                                    color={
                                      item.severity === 'Critical'
                                        ? 'error'
                                        : item.severity === 'Warning'
                                          ? 'warning'
                                          : 'default'
                                    }
                                  >
                                    {item.severity}
                                  </Tag>
                                  <Typography.Text>{item.message}</Typography.Text>
                                </Space>
                              </List.Item>
                            )}
                          />
                        )}
                      </Col>
                      <Col xs={24} lg={12}>
                        <Typography.Text strong>
                          {t('tseFailover.complianceRecommendations')}
                        </Typography.Text>
                        <List
                          size="small"
                          style={{ marginTop: 8 }}
                          dataSource={compliance?.recommendations ?? []}
                          renderItem={(item) => (
                            <List.Item>
                              <Typography.Text>{item.message}</Typography.Text>
                            </List.Item>
                          )}
                        />
                      </Col>
                    </Row>
                  </Space>
                );
              })()}
            </Card>

            <Card
              size="small"
              type="inner"
              title={t('tseFailover.performanceTitle')}
              extra={
                <Select
                  allowClear={false}
                  style={{ minWidth: 220 }}
                  placeholder={t('tseFailover.performanceDevice')}
                  value={effectivePerfDeviceId}
                  onChange={(v) => setPerfDeviceId(v)}
                  options={tenantDevices.map((d) => ({
                    value: d.id,
                    label: `${d.serialNumber}${d.isPrimary ? ' (P)' : d.isBackup ? ' (B)' : ''}`,
                  }))}
                />
              }
            >
              {performanceAnomalyQuery.data?.hasAnomaly ? (
                <Alert
                  type={
                    performanceAnomalyQuery.data.severity === 'Critical' ? 'error' : 'warning'
                  }
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('tseFailover.performanceAnomalyTitle')}
                  description={performanceAnomalyQuery.data.message}
                />
              ) : null}

              <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceAvgMs')}
                    value={performanceQuery.data?.averageResponseTime ?? 0}
                    precision={0}
                    suffix="ms"
                  />
                </Col>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceMaxMs')}
                    value={performanceQuery.data?.maxResponseTime ?? 0}
                    precision={0}
                    suffix="ms"
                  />
                </Col>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceSuccessRate')}
                    value={performanceQuery.data?.successRate ?? 0}
                    precision={1}
                    suffix="%"
                  />
                </Col>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceErrorRate')}
                    value={performanceQuery.data?.errorRate ?? 0}
                    precision={1}
                    suffix="%"
                  />
                </Col>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceTotal')}
                    value={performanceQuery.data?.totalRequests ?? 0}
                  />
                </Col>
                <Col xs={12} sm={8} md={4}>
                  <Statistic
                    title={t('tseFailover.performanceFailed')}
                    value={performanceQuery.data?.failedRequests ?? 0}
                  />
                </Col>
              </Row>

              <TsePerformanceChart
                data={performanceQuery.data?.performanceHistory}
                loading={performanceQuery.isLoading}
                slowThresholdMs={performanceQuery.data?.slowThresholdMs ?? 3000}
                criticalThresholdMs={performanceQuery.data?.criticalThresholdMs ?? 10000}
              />
            </Card>

            <Card size="small" type="inner" title={t('tseFailover.predictTitle')}>
              {(() => {
                const prediction = predictionQuery.data;
                const forecast = healthForecastQuery.data;
                const probability = prediction?.failureProbability ?? 0;
                const riskColor =
                  probability > 70 ? '#cf1322' : probability > 40 ? '#faad14' : '#52c41a';
                return (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text type="secondary">
                      {t('tseFailover.predictHeuristicNote')}
                    </Typography.Text>

                    {prediction?.riskLevel === 'Critical' || prediction?.requiresImmediateAction ? (
                      <Alert
                        type="error"
                        showIcon
                        message={t('tseFailover.predictCriticalTitle')}
                        description={t('tseFailover.predictCriticalDescription', {
                          probability: Math.round(probability),
                        })}
                      />
                    ) : prediction?.riskLevel === 'High' ? (
                      <Alert
                        type="warning"
                        showIcon
                        message={t('tseFailover.predictHighTitle')}
                        description={prediction.recommendations?.[0]}
                      />
                    ) : null}

                    <Progress
                      percent={Math.min(100, Math.round(probability))}
                      strokeColor={riskColor}
                      format={(percent) =>
                        t('tseFailover.predictFailureRiskFormat', { percent: percent ?? 0 })
                      }
                    />

                    <Row gutter={[16, 16]}>
                      <Col xs={12} sm={8} md={6}>
                        <Statistic
                          title={t('tseFailover.predictRiskLevel')}
                          value={prediction?.riskLevel ?? '—'}
                        />
                      </Col>
                      <Col xs={12} sm={8} md={6}>
                        <Statistic
                          title={t('tseFailover.predictHealthScore')}
                          value={prediction?.currentHealthScore ?? 0}
                        />
                      </Col>
                      <Col xs={12} sm={8} md={6}>
                        <Statistic
                          title={t('tseFailover.predictTrend')}
                          value={prediction?.healthTrendPerDay ?? 0}
                          precision={2}
                          suffix="/day"
                        />
                      </Col>
                      <Col xs={12} sm={8} md={6}>
                        <Statistic
                          title={t('tseFailover.predictFailureDate')}
                          value={
                            prediction?.predictedFailureDate
                              ? formatDate(prediction.predictedFailureDate)
                              : t('tseFailover.predictFailureDateNone')
                          }
                        />
                      </Col>
                    </Row>

                    {forecast ? (
                      <Typography.Text type="secondary">
                        {t('tseFailover.predictForecastHint', {
                          days: forecast.forecastDays,
                          score: forecast.predictedHealthScoreAtHorizon,
                          risk: forecast.predictedRiskLevel,
                        })}
                      </Typography.Text>
                    ) : null}

                    <Typography.Title level={5} style={{ marginBottom: 0 }}>
                      {t('tseFailover.predictRiskFactors')}
                    </Typography.Title>
                    <List
                      size="small"
                      loading={predictionQuery.isLoading}
                      dataSource={prediction?.riskFactors ?? []}
                      locale={{ emptyText: t('tseFailover.predictRiskFactorsEmpty') }}
                      renderItem={(factor) => (
                        <List.Item>
                          <Space direction="vertical" size={4} style={{ width: '100%' }}>
                            <Space
                              style={{ width: '100%', justifyContent: 'space-between' }}
                              wrap
                            >
                              <Typography.Text strong>{factor.name}</Typography.Text>
                              <Progress
                                percent={Math.min(100, Math.round(factor.impact))}
                                size="small"
                                style={{ width: 120, margin: 0 }}
                                strokeColor={factor.impact > 70 ? '#cf1322' : '#faad14'}
                                format={(p) => `${p}%`}
                              />
                            </Space>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {factor.description}
                            </Typography.Text>
                            {factor.isActionable && factor.recommendedAction ? (
                              <Tag color="blue">{factor.recommendedAction}</Tag>
                            ) : null}
                          </Space>
                        </List.Item>
                      )}
                    />
                  </Space>
                );
              })()}
            </Card>

            <Card size="small" type="inner" title={t('tseFailover.costTitle')}>
              {(() => {
                const cost = costReportQuery.data;
                return (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text type="secondary">
                      {t('tseFailover.costIndicativeNote')}
                    </Typography.Text>

                    {costAnomalyQuery.data?.hasAnomaly || cost?.hasCostAnomaly ? (
                      <Alert
                        type={
                          costAnomalyQuery.data?.severity === 'Critical' ? 'error' : 'warning'
                        }
                        showIcon
                        message={t('tseFailover.costAnomalyTitle')}
                        description={
                          costAnomalyQuery.data?.message ||
                          cost?.anomalyDescription ||
                          undefined
                        }
                      />
                    ) : null}

                    <Row gutter={[16, 16]}>
                      <Col xs={24} sm={12} md={8}>
                        <Statistic
                          title={t('tseFailover.costTotal')}
                          value={cost?.totalCost ?? 0}
                          precision={2}
                          prefix="€"
                        />
                        <Statistic
                          title={t('tseFailover.costAvgPerTx')}
                          value={cost?.averageCostPerTransaction ?? 0}
                          precision={4}
                          prefix="€"
                          style={{ marginTop: 16 }}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={8}>
                        <Statistic
                          title={t('tseFailover.costPotentialSavings')}
                          value={cost?.potentialSavings ?? 0}
                          precision={2}
                          prefix="€"
                          valueStyle={{ color: '#cf1322' }}
                        />
                        <Statistic
                          title={t('tseFailover.costTransactions')}
                          value={cost?.totalTransactions ?? 0}
                          style={{ marginTop: 16 }}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={8}>
                        <Statistic
                          title={t('tseFailover.costActiveDevices')}
                          value={cost?.activeDeviceCount ?? 0}
                        />
                        <Statistic
                          title={t('tseFailover.costBackupDevices')}
                          value={cost?.backupDeviceCount ?? 0}
                          style={{ marginTop: 16 }}
                        />
                      </Col>
                    </Row>

                    {(cost?.recommendations?.length ?? 0) === 0 ? (
                      <Typography.Text type="secondary">
                        {t('tseFailover.costRecommendationsEmpty')}
                      </Typography.Text>
                    ) : (
                      (cost?.recommendations ?? []).map((rec) => (
                        <Alert
                          key={rec.code}
                          type={
                            rec.severity === 'Critical'
                              ? 'error'
                              : rec.severity === 'Warning'
                                ? 'warning'
                                : 'info'
                          }
                          showIcon
                          message={rec.title}
                          description={
                            rec.estimatedMonthlySavings > 0
                              ? `${rec.description} (${t('tseFailover.costEstMonthlySavings')}: €${rec.estimatedMonthlySavings.toFixed(2)})`
                              : rec.description
                          }
                          style={{ marginTop: 8 }}
                        />
                      ))
                    )}
                  </Space>
                );
              })()}
            </Card>

            <Row gutter={[16, 16]}>
              <Col xs={24} lg={12}>
                <Card size="small" title={t('tseFailover.recommendationsTitle')} type="inner">
                  {(report?.recommendations?.length ?? 0) === 0 ? (
                    <Typography.Text type="secondary">
                      {t('tseFailover.recommendationsEmpty')}
                    </Typography.Text>
                  ) : (
                    <List
                      size="small"
                      dataSource={report?.recommendations ?? []}
                      renderItem={(item) => (
                        <List.Item>
                          <Space align="start">
                            <Tag
                              color={
                                item.severity === 'Critical'
                                  ? 'error'
                                  : item.severity === 'Warning'
                                    ? 'warning'
                                    : 'default'
                              }
                            >
                              {item.severity}
                            </Tag>
                            <Typography.Text>{item.message}</Typography.Text>
                          </Space>
                        </List.Item>
                      )}
                    />
                  )}
                </Card>
              </Col>
              <Col xs={24} lg={12}>
                <Card size="small" title={t('tseFailover.alertsTitle')} type="inner">
                  {(report?.recentAlerts?.length ?? 0) === 0 ? (
                    <Typography.Text type="secondary">{t('tseFailover.alertsEmpty')}</Typography.Text>
                  ) : (
                    <List
                      size="small"
                      dataSource={report?.recentAlerts ?? []}
                      renderItem={(item) => (
                        <List.Item>
                          <Space direction="vertical" size={0} style={{ width: '100%' }}>
                            <Typography.Text strong>{item.title}</Typography.Text>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {formatUtcDateTime(item.atUtc)} · {item.type}
                            </Typography.Text>
                            {item.description ? (
                              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {item.description}
                              </Typography.Text>
                            ) : null}
                          </Space>
                        </List.Item>
                      )}
                    />
                  )}
                </Card>
              </Col>
            </Row>
          </Space>
        )}
      </Card>

      <Card title={t('tseFailover.tableTitle')} style={{ marginBottom: 16 }}>
        <Table<TseFailoverDevice>
          rowKey="id"
          loading={devicesQuery.isLoading}
          dataSource={devices}
          columns={columns}
          pagination={{ pageSize: 20, showSizeChanger: true }}
          scroll={{ x: 960 }}
        />
      </Card>

      <Card title={t('tseFailover.historyTitle')}>
        {history.length === 0 && !historyQuery.isLoading ? (
          <Typography.Text type="secondary">{t('tseFailover.historyEmpty')}</Typography.Text>
        ) : (
          <Timeline
            items={history.map((item) => ({
              color: item.isSuccessful ? 'green' : 'red',
              children: (
                <div>
                  <Typography.Text strong>
                    {item.failoverType} {t('tseFailover.historyFailover')}
                  </Typography.Text>
                  <div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {item.primaryDeviceId}
                      {item.backupDeviceId ? ` → ${item.backupDeviceId}` : ''}
                    </Typography.Text>
                  </div>
                  <div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {formatUtcDateTime(item.startedAt)} · {item.triggerReason}
                    </Typography.Text>
                  </div>
                  {item.errorMessage ? (
                    <Typography.Text type="danger" style={{ fontSize: 12 }}>
                      {item.errorMessage}
                    </Typography.Text>
                  ) : null}
                </div>
              ),
            }))}
          />
        )}
      </Card>

      <Modal
        title={t('tseFailover.manualModalTitle')}
        open={manualPrimaryId != null}
        onCancel={() => {
          setManualPrimaryId(null);
          setManualBackupId(undefined);
        }}
        onOk={() => {
          if (!manualPrimaryId || !manualBackupId) {
            notify.error(t('tseFailover.manualBackupRequired'));
            return;
          }
          manualMutation.mutate({
            primaryDeviceId: manualPrimaryId,
            backupDeviceId: manualBackupId,
          });
        }}
        okText={t('tseFailover.manualConfirm')}
        confirmLoading={manualMutation.isPending}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{t('tseFailover.manualModalHint')}</Typography.Paragraph>
        {backupOptions.length === 0 ? (
          <Alert type="warning" showIcon message={t('tseFailover.manualNoBackups')} />
        ) : (
          <Select
            style={{ width: '100%' }}
            placeholder={t('tseFailover.manualBackupLabel')}
            options={backupOptions}
            value={manualBackupId}
            onChange={setManualBackupId}
          />
        )}
      </Modal>
    </div>
  );
}
