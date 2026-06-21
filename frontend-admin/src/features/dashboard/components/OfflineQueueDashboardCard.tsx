'use client';

import React from 'react';
import { Alert, Card, Col, Row, Space, Statistic, Typography } from 'antd';
import { InboxOutlined, LinkOutlined } from '@ant-design/icons';
import NextLink from 'next/link';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { formatDateTime } from '@/i18n/formatting';
import { useI18n } from '@/i18n/I18nProvider';
import {
    getApiAdminOfflineTransactionsSummary,
    getGetApiAdminOfflineTransactionsSummaryQueryKey,
} from '@/api/generated/admin/admin';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const REFETCH_MS = 30_000;

/**
 * Dashboard signal block: TSE offline replay queue (server-queued non-fiscal intents).
 */
export function OfflineQueueDashboardCard() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const enabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);

    const summaryQuery = useAuthorizedQuery({
        queryKey: getGetApiAdminOfflineTransactionsSummaryQueryKey(),
        queryFn: ({ signal }) => getApiAdminOfflineTransactionsSummary({ signal }),
        requiredPermission: PERMISSIONS.PAYMENT_VIEW,
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
                    <span>{t('dashboard.offlineQueue.title')}</span>
                    <NextLink href="/admin/tse/offline-transactions" style={{ fontWeight: 500 }}>
                        <LinkOutlined /> {t('dashboard.offlineQueue.manage')}
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
                    title={t('dashboard.offlineQueue.backlog_title')}
                    description={t('dashboard.offlineQueue.backlog_description', { pending })}
                />
            ) : null}
            <Row gutter={16}>
                <Col xs={24} sm={8}>
                    <Statistic
                        title={t('dashboard.offlineQueue.pending')}
                        value={pending}
                        loading={summaryQuery.isLoading}
                    />
                </Col>
                <Col xs={24} sm={8}>
                    <Statistic
                        title={t('dashboard.offlineQueue.failed')}
                        value={failed}
                        loading={summaryQuery.isLoading}
                    />
                </Col>
                <Col xs={24} sm={8}>
                    <Typography.Text type="secondary">
                        {t('dashboard.offlineQueue.last_replay_utc')}
                    </Typography.Text>
                    <div>
                        <Typography.Text strong>
                            {lastReplay
                                ? formatDateTime(lastReplay, '', { second: '2-digit' })
                                : '—'}
                        </Typography.Text>
                    </div>
                </Col>
            </Row>
        </Card>
    );
}
