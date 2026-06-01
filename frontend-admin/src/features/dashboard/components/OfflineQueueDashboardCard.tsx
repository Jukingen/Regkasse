'use client';

import React from 'react';
import { Alert, Card, Col, Row, Space, Statistic, Typography } from 'antd';
import { InboxOutlined, LinkOutlined } from '@ant-design/icons';
import NextLink from 'next/link';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import {
    getApiAdminOfflineTransactionsSummary,
    getGetApiAdminOfflineTransactionsSummaryQueryKey,
} from '@/api/generated/admin/admin';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

dayjs.extend(utc);

const REFETCH_MS = 30_000;

/**
 * Dashboard signal block: TSE offline replay queue (server-queued non-fiscal intents).
 * German operator copy; auto-refresh to match /admin/tse/offline-transactions page.
 */
export function OfflineQueueDashboardCard() {
    const { hasPermission } = usePermissions();
    const enabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);

    const summaryQuery = useQuery({
        queryKey: getGetApiAdminOfflineTransactionsSummaryQueryKey(),
        queryFn: ({ signal }) => getApiAdminOfflineTransactionsSummary({ signal }),
        enabled,
        staleTime: 60_000,
        refetchInterval: REFETCH_MS,
        refetchIntervalInBackground: false,
        refetchOnWindowFocus: false,
    });

    if (!enabled) return null;

    const pending = summaryQuery.data?.pendingCount ?? 0;
    const failed = summaryQuery.data?.failedCount ?? 0;
    const lastReplay = summaryQuery.data?.lastReplayAtUtc;
    const backlogHigh = pending > 10;

    return (
        <Card
            title={
                <Space>
                    <InboxOutlined />
                    <span>Offline-Warteschlange (TSE)</span>
                    <NextLink href="/admin/tse/offline-transactions" style={{ fontWeight: 500 }}>
                        <LinkOutlined /> Verwalten
                    </NextLink>
                </Space>
            }
            style={{ marginBottom: 24 }}
        >
            {backlogHigh ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 12 }}
                    title="Erhöhter Rückstau"
                    description={`${pending} Zahlungen warten auf fiskalische Signatur (Schwelle &gt; 10).`}
                />
            ) : null}
            <Row gutter={16}>
                <Col xs={24} sm={8}>
                    <Statistic title="Ausstehend" value={pending} loading={summaryQuery.isLoading} />
                </Col>
                <Col xs={24} sm={8}>
                    <Statistic title="Fehlgeschlagen" value={failed} loading={summaryQuery.isLoading} />
                </Col>
                <Col xs={24} sm={8}>
                    <Typography.Text type="secondary">Letzter Replay (UTC)</Typography.Text>
                    <div>
                        <Typography.Text strong>
                            {lastReplay
                                ? dayjs(lastReplay).utc().format('DD.MM.YYYY HH:mm:ss')
                                : '—'}
                        </Typography.Text>
                    </div>
                </Col>
            </Row>
        </Card>
    );
}
