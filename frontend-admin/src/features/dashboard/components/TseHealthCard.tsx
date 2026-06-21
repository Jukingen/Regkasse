'use client';

import React, { useMemo } from 'react';
import { Alert, Badge, Card, Col, Progress, Row, Statistic, Typography } from 'antd';
import { useGetApiTseHealth } from '@/api/generated/tse/tse';
import type { TseHealthResponseDto } from '@/api/generated/model';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { formatDateTime } from '@/i18n/formatting';
import { useI18n } from '@/i18n/I18nProvider';
import { AppPermissions } from '@/shared/auth/permissions';

const REFETCH_MS = 30_000;

function healthPercentFromSnapshot(data: TseHealthResponseDto | undefined): number {
    const status = data?.status ?? '';
    const failures = data?.consecutiveFailures ?? 0;
    if (status === 'Online') return 100;
    if (status === 'Offline') return Math.max(0, Math.min(20, 15 - Math.min(failures, 10)));
    if (status === 'Degraded') return Math.max(30, 100 - Math.min(failures * 15, 65));
    return 55;
}

/**
 * Cached TSE operational health from `/api/tse/health` (background probe snapshot).
 */
export function TseHealthCard() {
    const { t } = useI18n();
    const { isAuthorized } = useAuthorizationGate({
        requiredPermission: AppPermissions.CashRegisterView,
    });
    const { data, isLoading } = useGetApiTseHealth(undefined, {
        query: {
            enabled: isAuthorized,
            refetchInterval: REFETCH_MS,
            refetchIntervalInBackground: false,
            refetchOnWindowFocus: false,
            staleTime: 60_000,
        },
    });

    const statusBadge = (snapshot: TseHealthResponseDto | undefined) => {
        switch (snapshot?.status) {
            case 'Online':
                return <Badge status="success" text={t('dashboard.tseHealth.status_healthy')} />;
            case 'Degraded':
                return <Badge status="warning" text={t('dashboard.tseHealth.status_degraded')} />;
            case 'Offline':
                return <Badge status="error" text={t('dashboard.tseHealth.status_offline')} />;
            default:
                return <Badge status="default" text={t('dashboard.tseHealth.status_unknown')} />;
        }
    };

    if (!isAuthorized) {
        return null;
    }

    const healthPercent = useMemo(() => healthPercentFromSnapshot(data), [data]);

    const nextProbeHint =
        data?.estimatedRecoveryTimeUtc != null
            ? formatDateTime(data.estimatedRecoveryTimeUtc, '')
            : null;

    const failureDescription = (snapshot: TseHealthResponseDto) => (
        <>
            {t('dashboard.tseHealth.failures_in_row', { count: snapshot.consecutiveFailures ?? 0 })}
            {nextProbeHint ? (
                <>
                    {' '}
                    {t('dashboard.tseHealth.next_probe_eta', { time: nextProbeHint })}
                </>
            ) : null}
            {snapshot.lastErrorMessageSafe ? (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                    {snapshot.lastErrorMessageSafe}
                </Typography.Paragraph>
            ) : null}
        </>
    );

    return (
        <Card title={t('dashboard.tseHealth.title')} loading={isLoading} style={{ marginBottom: 24 }}>
            <Row gutter={16}>
                <Col xs={24} sm={12}>
                    <Statistic
                        title={t('dashboard.tseHealth.current_status')}
                        valueRender={() => statusBadge(data)}
                    />
                </Col>
                <Col xs={24} sm={12}>
                    <Statistic
                        title={t('dashboard.tseHealth.last_successful_check')}
                        value={
                            data?.lastSuccessfulPingUtc
                                ? formatDateTime(data.lastSuccessfulPingUtc, '')
                                : '—'
                        }
                    />
                </Col>
            </Row>

            <Row gutter={16} style={{ marginTop: 16 }}>
                <Col xs={24} sm={12}>
                    <Statistic
                        title={t('dashboard.tseHealth.last_check_utc')}
                        value={
                            data?.lastCheckUtc ? formatDateTime(data.lastCheckUtc, '') : '—'
                        }
                    />
                </Col>
                <Col xs={24} sm={12}>
                    <Statistic
                        title={t('dashboard.tseHealth.consecutive_failures')}
                        value={data?.consecutiveFailures ?? 0}
                    />
                </Col>
            </Row>

            {data?.status === 'Degraded' && (
                <Alert
                    type="warning"
                    title={t('dashboard.tseHealth.degraded_title')}
                    description={failureDescription(data)}
                    style={{ marginTop: 16 }}
                    showIcon
                />
            )}

            {data?.status === 'Offline' && (
                <Alert
                    type="error"
                    title={t('dashboard.tseHealth.offline_title')}
                    description={failureDescription(data)}
                    style={{ marginTop: 16 }}
                    showIcon
                />
            )}

            <Progress
                percent={healthPercent}
                status={healthPercent < 70 ? 'exception' : 'active'}
                style={{ marginTop: 16 }}
            />
        </Card>
    );
}
