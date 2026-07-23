'use client';

/**
 * Super Admin monitoring dashboard — live client API metrics + health probes + external links.
 */
import { ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Progress, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import { useCallback, useEffect, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useI18n } from '@/i18n';
import { formatTime } from '@/lib/dateUtils';
import {
  type ApiMetricSample,
  type ApiMetricsSummary,
  getApiMetricSamples,
  getApiMetricsSummary,
  subscribeApiMetrics,
} from '@/lib/monitoring/apiMetricsStore';
import { MONITORING_THRESHOLDS } from '@/lib/monitoring/thresholds';

type HealthPayload = {
  status?: string;
  uptimeSec?: number;
  env?: string;
  checks?: { sentryConfigured?: boolean };
};

function formatPct(rate: number): string {
  return `${(rate * 100).toFixed(1)}%`;
}

function formatMs(value: number | null): string {
  if (value == null) {
    return '—';
  }
  return `${Math.round(value)} ms`;
}

export default function AdminMonitoringPage() {
  const { t } = useI18n();
  const [summary, setSummary] = useState<ApiMetricsSummary>(() => getApiMetricsSummary());
  const [samples, setSamples] = useState<readonly ApiMetricSample[]>(() => getApiMetricSamples());
  const [faHealth, setFaHealth] = useState<HealthPayload | null>(null);
  const [faHealthError, setFaHealthError] = useState<string | null>(null);
  const [healthLoading, setHealthLoading] = useState(false);

  const refreshLocal = useCallback(() => {
    setSummary(getApiMetricsSummary());
    setSamples([...getApiMetricSamples()].slice(-25).reverse());
  }, []);

  const probeHealth = useCallback(async () => {
    setHealthLoading(true);
    setFaHealthError(null);
    try {
      const res = await fetch('/api/monitoring/health', { cache: 'no-store' });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      const json = (await res.json()) as HealthPayload;
      setFaHealth(json);
    } catch (err) {
      setFaHealth(null);
      setFaHealthError(err instanceof Error ? err.message : 'health_failed');
    } finally {
      setHealthLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshLocal();
    void probeHealth();
    return subscribeApiMetrics(refreshLocal);
  }, [probeHealth, refreshLocal]);

  const errorRatePct = Math.min(100, summary.errorRate * 100);
  const errorAlert = summary.errorRateAlert;

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('monitoring.pageTitle')}
        subtitle={t('monitoring.pageSubtitle')}
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={refreshLocal}>
              {t('monitoring.refreshMetrics')}
            </Button>
            <Button loading={healthLoading} onClick={() => void probeHealth()}>
              {t('monitoring.probeHealth')}
            </Button>
          </Space>
        }
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('monitoring.primarySinkTitle')}
        description={<span>{t('monitoring.primarySinkBody')}</span>}
      />

      {(errorAlert || summary.hasSlowRequests) && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message={t('monitoring.sloBreachTitle')}
          description={
            errorAlert
              ? t('monitoring.sloErrorRate', {
                  rate: formatPct(summary.errorRate),
                  threshold: formatPct(MONITORING_THRESHOLDS.apiErrorRate),
                })
              : t('monitoring.sloSlowRequests', {
                  count: summary.slowCount,
                  thresholdMs: MONITORING_THRESHOLDS.apiResponseTimeMs,
                })
          }
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card size="small" title={t('monitoring.cardErrorRate')}>
            <Statistic
              value={errorRatePct}
              precision={1}
              suffix="%"
              valueStyle={{ color: errorAlert ? '#cf1322' : undefined }}
            />
            <Progress
              percent={errorRatePct}
              showInfo={false}
              status={errorAlert ? 'exception' : 'normal'}
              strokeColor={errorAlert ? '#cf1322' : undefined}
            />
            <Typography.Text type="secondary">
              {t('monitoring.samplesInWindow', {
                count: summary.sampleCount,
                minutes: Math.round(summary.windowMs / 60000),
              })}
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small" title={t('monitoring.cardLatency')}>
            <Space size="large" wrap>
              <Statistic title="p50" value={formatMs(summary.p50Ms)} />
              <Statistic
                title="p95"
                value={formatMs(summary.p95Ms)}
                valueStyle={{
                  color:
                    summary.p95Ms != null &&
                    summary.p95Ms > MONITORING_THRESHOLDS.apiResponseTimeMs
                      ? '#cf1322'
                      : undefined,
                }}
              />
              <Statistic title="p99" value={formatMs(summary.p99Ms)} />
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
              {t('monitoring.latencyThreshold', {
                ms: MONITORING_THRESHOLDS.apiResponseTimeMs,
              })}
            </Typography.Paragraph>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small" title={t('monitoring.cardHealth')}>
            {faHealthError ? (
              <Tag color="error">{t('monitoring.healthDown')}</Tag>
            ) : faHealth?.status === 'ok' ? (
              <Tag color="success">{t('monitoring.healthOk')}</Tag>
            ) : (
              <Tag>{t('monitoring.healthUnknown')}</Tag>
            )}
            <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
              {t('monitoring.uptimeSec', { sec: faHealth?.uptimeSec ?? '—' })}
              <br />
              {t('monitoring.envLabel', { env: faHealth?.env ?? '—' })}
              <br />
              Sentry:{' '}
              {faHealth?.checks?.sentryConfigured ? (
                <Tag color="blue">{t('monitoring.configured')}</Tag>
              ) : (
                <Tag>{t('monitoring.notConfigured')}</Tag>
              )}
            </Typography.Paragraph>
            {faHealthError ? (
              <Typography.Text type="danger">{faHealthError}</Typography.Text>
            ) : null}
          </Card>
        </Col>
      </Row>

      <Card size="small" title={t('monitoring.recentCalls')} style={{ marginTop: 16 }}>
        <Table
          size="small"
          rowKey={(row) => `${row.at}-${row.method}-${row.path}-${row.status}`}
          pagination={false}
          dataSource={samples as ApiMetricSample[]}
          columns={[
            {
              title: t('monitoring.colTime'),
              dataIndex: 'at',
              render: (at: number) => formatTime(at),
            },
            { title: t('monitoring.colMethod'), dataIndex: 'method', width: 80 },
            { title: t('monitoring.colPath'), dataIndex: 'path', ellipsis: true },
            {
              title: t('monitoring.colStatus'),
              dataIndex: 'status',
              width: 80,
              render: (status: number, row: ApiMetricSample) => (
                <Tag color={row.ok ? 'success' : 'error'}>{status || '—'}</Tag>
              ),
            },
            {
              title: t('monitoring.colDuration'),
              dataIndex: 'durationMs',
              width: 100,
              render: (ms: number) => (
                <span
                  style={{
                    color: ms > MONITORING_THRESHOLDS.apiResponseTimeMs ? '#cf1322' : undefined,
                  }}
                >
                  {Math.round(ms)} ms
                </span>
              ),
            },
          ]}
          locale={{ emptyText: t('monitoring.noSamples') }}
        />
      </Card>

      <Card size="small" title={t('monitoring.externalLinks')} style={{ marginTop: 16 }}>
        <Space wrap>
          <Typography.Link href="/health" target="_blank">
            GET /health
          </Typography.Link>
          <Typography.Link href="/api/monitoring/health" target="_blank">
            GET /api/monitoring/health
          </Typography.Link>
          <Typography.Text type="secondary">{t('monitoring.grafanaHint')}</Typography.Text>
        </Space>
      </Card>
    </AdminPageShell>
  );
}
