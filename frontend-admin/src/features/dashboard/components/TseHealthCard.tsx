'use client';

import React, { useMemo } from 'react';
import { Alert, Badge, Card, Col, Progress, Row, Statistic, Typography } from 'antd';
import { useGetApiTseHealth } from '@/api/generated/tse/tse';
import type { TseHealthResponseDto } from '@/api/generated/model';

const REFETCH_MS = 30_000;

function healthPercentFromSnapshot(data: TseHealthResponseDto | undefined): number {
    const status = data?.status ?? '';
    const failures = data?.consecutiveFailures ?? 0;
    if (status === 'Online') return 100;
    if (status === 'Offline') return Math.max(0, Math.min(20, 15 - Math.min(failures, 10)));
    if (status === 'Degraded') return Math.max(30, 100 - Math.min(failures * 15, 65));
    return 55;
}

function statusBadge(data: TseHealthResponseDto | undefined) {
    switch (data?.status) {
        case 'Online':
            return <Badge status="success" text="Gesund" />;
        case 'Degraded':
            return <Badge status="warning" text="Eingeschränkt" />;
        case 'Offline':
            return <Badge status="error" text="Offline" />;
        default:
            return <Badge status="default" text="Unbekannt" />;
    }
}

/**
 * Cached TSE operational health from `/api/tse/health` (background probe snapshot).
 */
export function TseHealthCard() {
    const { data, isLoading } = useGetApiTseHealth(undefined, {
        query: {
            refetchInterval: REFETCH_MS,
            refetchIntervalInBackground: false,
            refetchOnWindowFocus: false,
            staleTime: 60_000,
        },
    });

    const healthPercent = useMemo(() => healthPercentFromSnapshot(data), [data]);

    const nextProbeHint =
        data?.estimatedRecoveryTimeUtc != null
            ? new Date(data.estimatedRecoveryTimeUtc).toLocaleString('de-DE')
            : null;

    return (
        <Card title="TSE-Status" loading={isLoading} style={{ marginBottom: 24 }}>
            <Row gutter={16}>
                <Col xs={24} sm={12}>
                    <Statistic title="Aktueller Status" valueRender={() => statusBadge(data)} />
                </Col>
                <Col xs={24} sm={12}>
                    <Statistic
                        title="Letzter erfolgreicher TSE-Check"
                        value={
                            data?.lastSuccessfulPingUtc
                                ? new Date(data.lastSuccessfulPingUtc).toLocaleString('de-DE')
                                : '—'
                        }
                    />
                </Col>
            </Row>

            <Row gutter={16} style={{ marginTop: 16 }}>
                <Col xs={24} sm={12}>
                    <Statistic
                        title="Letzte Prüfung (UTC)"
                        value={
                            data?.lastCheckUtc ? new Date(data.lastCheckUtc).toLocaleString('de-DE') : '—'
                        }
                    />
                </Col>
                <Col xs={24} sm={12}>
                    <Statistic title="Aufeinanderfolgende Fehler" value={data?.consecutiveFailures ?? 0} />
                </Col>
            </Row>

            {data?.status === 'Degraded' && (
                <Alert
                    type="warning"
                    title="TSE eingeschränkt"
                    description={
                        <>
                            Fehler in Folge: {data.consecutiveFailures ?? 0}.
                            {nextProbeHint ? (
                                <>
                                    {' '}
                                    Nächste geplante Prüfung (ETA): {nextProbeHint}.
                                </>
                            ) : null}
                            {data.lastErrorMessageSafe ? (
                                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                                    {data.lastErrorMessageSafe}
                                </Typography.Paragraph>
                            ) : null}
                        </>
                    }
                    style={{ marginTop: 16 }}
                    showIcon
                />
            )}

            {data?.status === 'Offline' && (
                <Alert
                    type="error"
                    title="TSE offline"
                    description={
                        <>
                            Fehler in Folge: {data.consecutiveFailures ?? 0}.
                            {nextProbeHint ? (
                                <>
                                    {' '}
                                    Nächste geplante Prüfung (ETA): {nextProbeHint}.
                                </>
                            ) : null}
                            {data.lastErrorMessageSafe ? (
                                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                                    {data.lastErrorMessageSafe}
                                </Typography.Paragraph>
                            ) : null}
                        </>
                    }
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
