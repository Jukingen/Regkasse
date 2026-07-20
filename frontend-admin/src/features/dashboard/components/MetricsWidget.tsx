'use client';

import { Card, Col, Progress, Row, Space, Statistic, Tag } from 'antd';
import { useI18n } from '@/i18n/I18nProvider';
import { useMetrics } from '@/features/metrics/hooks/useMetrics';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function MetricsWidget({ title, dragHandleProps, onRefresh }: Props) {
    const { t } = useI18n();
    const { data: metrics, isLoading, isError, refetch } = useMetrics();

    const handleRefresh = () => {
        void refetch();
        onRefresh?.();
    };

    if (isLoading && !metrics) {
        return (
            <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
                <Card loading variant="borderless" styles={{ body: { padding: 0 } }} />
            </WidgetShell>
        );
    }

    if (isError || !metrics) {
        return (
            <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
                <span>{t('dashboard.metricsWidget.loadFailed')}</span>
            </WidgetShell>
        );
    }

    const uptime = metrics.uptime || 0;
    const uptimeDays = Math.floor(uptime / 86400);
    const uptimeHours = Math.floor((uptime % 86400) / 3600);
    const uptimeMinutes = Math.floor((uptime % 3600) / 60);
    const avg = metrics.avgResponseTime || 0;
    const errorRate = metrics.errorRate || 0;

    return (
        <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
            <div style={{ marginBottom: 12 }}>
                <Tag color="success">{t('dashboard.metricsWidget.operational')}</Tag>
            </div>

            <Row gutter={[16, 16]}>
                <Col xs={12} sm={6}>
                    <Statistic
                        title={t('dashboard.metricsWidget.requests')}
                        value={metrics.totalRequests || 0}
                        suffix={t('dashboard.metricsWidget.requestsSuffix')}
                    />
                </Col>
                <Col xs={12} sm={6}>
                    <Statistic
                        title={t('dashboard.metricsWidget.avgResponse')}
                        value={avg}
                        suffix="ms"
                        valueStyle={{ color: avg < 200 ? '#16a34a' : '#eab308' }}
                    />
                </Col>
                <Col xs={12} sm={6}>
                    <Statistic
                        title={t('dashboard.metricsWidget.errorRate')}
                        value={errorRate}
                        suffix="%"
                        valueStyle={{ color: errorRate < 5 ? '#16a34a' : '#dc2626' }}
                    />
                </Col>
                <Col xs={12} sm={6}>
                    <Statistic
                        title={t('dashboard.metricsWidget.activeUsers')}
                        value={metrics.activeUsers || 0}
                    />
                </Col>
            </Row>

            <div style={{ marginTop: 16 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                    <span>{t('dashboard.metricsWidget.uptime')}</span>
                    <span>
                        {t('dashboard.metricsWidget.uptimeValue', {
                            days: uptimeDays,
                            hours: uptimeHours,
                            minutes: uptimeMinutes,
                        })}
                    </span>
                </div>
                <Progress percent={100} status="active" strokeColor="#16a34a" showInfo={false} />
            </div>

            <div style={{ marginTop: 12 }}>
                <Space wrap>
                    <Tag color="blue">
                        {t('dashboard.metricsWidget.cacheHit', {
                            percent: metrics.cacheHitRatio || 0,
                        })}
                    </Tag>
                    <Tag color="cyan">
                        {t('dashboard.metricsWidget.activeOrders', {
                            count: metrics.activeOrders || 0,
                        })}
                    </Tag>
                    <Tag color="purple">
                        {t('dashboard.metricsWidget.tenants', {
                            count: metrics.activeTenants || 0,
                        })}
                    </Tag>
                </Space>
            </div>
        </WidgetShell>
    );
}
