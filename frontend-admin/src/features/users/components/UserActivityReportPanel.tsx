'use client';

/**
 * Compliance user activity report (login, sessions, action summary, timeline).
 */
import React, { useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Col,
    DatePicker,
    Descriptions,
    Row,
    Space,
    Statistic,
    Table,
    Tag,
    Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';

import { fetchUserActivityReport } from '@/features/users/api/userActivityReport';
import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { usersCopy } from '@/features/users/constants/copy';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

const { Text } = Typography;
const NA = '—';

type Props = {
    userId: string;
    userName?: string;
};

export function UserActivityReportPanel({ userId, userName }: Props) {
    const { t, formatLocale } = useI18n();
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
        dayjs().subtract(90, 'day'),
        dayjs(),
    ]);

    const params = useMemo(() => {
        const p: { startDate?: string; endDate?: string } = {};
        if (dateRange[0]) p.startDate = dateRange[0].format('YYYY-MM-DD');
        if (dateRange[1]) p.endDate = dateRange[1].format('YYYY-MM-DD');
        return p;
    }, [dateRange]);

    const validUserId = (userId ?? '').trim();
    const { data, isLoading, isError, refetch } = useQuery({
        queryKey: ['user-activity-report', validUserId, params],
        queryFn: () => fetchUserActivityReport(validUserId, params),
        enabled: validUserId.length > 0,
        staleTime: 60_000,
    });

    if (validUserId.length === 0) {
        return <Alert type="info" title={usersCopy.emptyActivity} showIcon />;
    }

    if (isError) {
        return (
            <Alert
                type="warning"
                title={usersCopy.errorLoadActivity}
                action={
                    <Button size="small" onClick={() => refetch()}>
                        {usersCopy.retry}
                    </Button>
                }
            />
        );
    }

    const actions = data?.actionsPerformed;

    const timelineColumns = [
        {
            title: usersCopy.activityTime,
            dataIndex: 'date',
            key: 'date',
            width: 170,
            render: (v: string) => (v ? formatDateTime(v, formatLocale) : NA),
        },
        {
            title: usersCopy.action,
            dataIndex: 'action',
            key: 'action',
            width: 160,
            render: (action: string) => {
                const labelKey = getAuditActionLabelKey(action);
                const label = labelKey
                    ? t(labelKey as 'common.auditLogs.actionLabels.login')
                    : action;
                return <Tag color="blue">{label}</Tag>;
            },
        },
        {
            title: usersCopy.activityEntityType,
            dataIndex: 'entityType',
            key: 'entityType',
            width: 120,
        },
        {
            title: usersCopy.ipAddress,
            dataIndex: 'ipAddress',
            key: 'ipAddress',
            width: 120,
            render: (v: string | null | undefined) => (v?.trim() ? v : NA),
        },
    ];

    return (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            {userName && (
                <Text type="secondary">
                    {usersCopy.activityReportFor}: {userName}
                </Text>
            )}

            <Space wrap>
                <Text type="secondary">{usersCopy.filterDateRange}:</Text>
                <DatePicker.RangePicker
                    value={dateRange[0] && dateRange[1] ? dateRange : null}
                    onChange={(dates) =>
                        setDateRange(dates ? [dates[0] ?? null, dates[1] ?? null] : [null, null])
                    }
                    format="DD.MM.YYYY"
                    allowClear={false}
                />
                <Button size="small" onClick={() => refetch()} loading={isLoading}>
                    {t('common.buttons.refresh')}
                </Button>
            </Space>

            <Card size="small" title={usersCopy.activityReportIdentity} loading={isLoading}>
                <Descriptions column={{ xs: 1, sm: 2 }} size="small">
                    <Descriptions.Item label={usersCopy.userName}>
                        {data?.userName ?? NA}
                    </Descriptions.Item>
                    <Descriptions.Item label={usersCopy.email}>{data?.email ?? NA}</Descriptions.Item>
                    <Descriptions.Item label={usersCopy.role}>
                        <Tag>{data?.role ?? NA}</Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('users.tabs.tenant.columnTenant')}>
                        {data?.tenantName?.trim() || NA}
                    </Descriptions.Item>
                </Descriptions>
            </Card>

            <Row gutter={[16, 16]}>
                <Col xs={24} sm={12} lg={6}>
                    <Card size="small" loading={isLoading}>
                        <Statistic
                            title={usersCopy.lastLogin}
                            value={
                                data?.lastLoginAt
                                    ? formatDateTime(data.lastLoginAt, formatLocale)
                                    : NA
                            }
                        />
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            {usersCopy.ipAddress}: {data?.lastLoginIp?.trim() || NA}
                        </Text>
                    </Card>
                </Col>
                <Col xs={12} sm={6} lg={3}>
                    <Card size="small" loading={isLoading}>
                        <Statistic title={usersCopy.activityReportTotalLogins} value={data?.totalLogins ?? 0} />
                    </Card>
                </Col>
                <Col xs={12} sm={6} lg={3}>
                    <Card size="small" loading={isLoading}>
                        <Statistic
                            title={usersCopy.activityReportFailedLogins}
                            value={data?.failedLoginAttempts ?? 0}
                            styles={
                                (data?.failedLoginAttempts ?? 0) > 0 ? { content: { color: '#cf1322' } } : undefined
                            }
                        />
                    </Card>
                </Col>
                <Col xs={12} sm={6} lg={4}>
                    <Card size="small" loading={isLoading}>
                        <Statistic
                            title={usersCopy.activityReportActiveSessions}
                            value={data?.activeSessions ?? 0}
                        />
                    </Card>
                </Col>
                <Col xs={12} sm={6} lg={4}>
                    <Card size="small" loading={isLoading}>
                        <Statistic
                            title={usersCopy.activityReportAvgSessionMin}
                            value={data?.averageSessionDurationMinutes ?? 0}
                            suffix="min"
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} lg={4}>
                    <Card size="small" loading={isLoading}>
                        <Statistic
                            title={usersCopy.activityReportLastSessionEnd}
                            value={
                                data?.lastSessionEndAt
                                    ? formatDateTime(data.lastSessionEndAt, formatLocale)
                                    : NA
                            }
                        />
                    </Card>
                </Col>
            </Row>

            <Card size="small" title={usersCopy.activityReportActionSummary} loading={isLoading}>
                <Row gutter={[16, 16]}>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic title={usersCopy.activityReportUserCreates} value={actions?.userCreates ?? 0} />
                    </Col>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic title={usersCopy.activityReportUserEdits} value={actions?.userEdits ?? 0} />
                    </Col>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic
                            title={usersCopy.activityReportPayments}
                            value={actions?.paymentsProcessed ?? 0}
                        />
                    </Col>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic title={usersCopy.activityReportStornos} value={actions?.stornos ?? 0} />
                    </Col>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic title={usersCopy.activityReportRefunds} value={actions?.refunds ?? 0} />
                    </Col>
                    <Col xs={12} sm={8} md={4}>
                        <Statistic title={usersCopy.activityReportExports} value={actions?.exports ?? 0} />
                    </Col>
                </Row>
            </Card>

            <Card size="small" title={usersCopy.activityReportTimeline} loading={isLoading}>
                <Table
                    size="small"
                    rowKey={(r, i) => `${r.date}-${r.action}-${i}`}
                    columns={timelineColumns}
                    dataSource={data?.activityTimeline ?? []}
                    pagination={{ pageSize: 10, showSizeChanger: false }}
                    scroll={{ x: 640 }}
                />
            </Card>
        </Space>
    );
}
