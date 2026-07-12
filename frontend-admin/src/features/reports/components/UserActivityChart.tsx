'use client';

/**
 * Activity timeline (bar) and action breakdown (pie) for user activity report.
 */
import React, { useMemo } from 'react';
import { Card, Col, Empty, Row } from 'antd';
import {
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Legend,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';
import { formatUserMonthDay } from '@/lib/dateFormatter';

import type { UserActivityActionSummary, UserActivityDailyCount } from '@/features/reports/api/userActivityReport';
import { useI18n } from '@/i18n';

const PIE_COLORS = ['#1677ff', '#52c41a', '#faad14', '#ff4d4f', '#722ed1', '#13c2c2'];

type Props = {
    dailyActivity: UserActivityDailyCount[];
    actionsPerformed: UserActivityActionSummary;
};

export function UserActivityChart({ dailyActivity, actionsPerformed }: Props) {
    const { t } = useI18n();

    const lineData = useMemo(
        () =>
            dailyActivity.map((d) => ({
                name: formatUserMonthDay(d.date),
                count: d.count,
            })),
        [dailyActivity],
    );

    const pieData = useMemo(() => {
        const a = actionsPerformed;
        return [
            { name: t('reporting.userActivity.userCreates'), value: a.userCreates },
            { name: t('reporting.userActivity.userEdits'), value: a.userEdits },
            { name: t('reporting.userActivity.payments'), value: a.paymentsProcessed },
            { name: t('reporting.userActivity.stornos'), value: a.stornos },
            { name: t('reporting.userActivity.refunds'), value: a.refunds },
            { name: t('reporting.userActivity.exports'), value: a.exports },
        ].filter((x) => x.value > 0);
    }, [actionsPerformed, t]);

    return (
        <Row gutter={[16, 16]}>
            <Col xs={24} lg={14}>
                <Card size="small" title={t('reporting.userActivity.activityOverTime')}>
                    {lineData.length === 0 ? (
                        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
                    ) : (
                        <ResponsiveContainer width="100%" height={280}>
                            <BarChart data={lineData} margin={{ top: 8, right: 12, left: 0, bottom: 4 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                                <YAxis allowDecimals={false} width={36} tick={{ fontSize: 11 }} />
                                <Tooltip />
                                <Bar dataKey="count" fill="#1677ff" radius={[4, 4, 0, 0]} maxBarSize={40} />
                            </BarChart>
                        </ResponsiveContainer>
                    )}
                </Card>
            </Col>
            <Col xs={24} lg={10}>
                <Card size="small" title={t('reporting.userActivity.actionBreakdown')}>
                    {pieData.length === 0 ? (
                        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
                    ) : (
                        <ResponsiveContainer width="100%" height={280}>
                            <PieChart>
                                <Pie
                                    data={pieData}
                                    dataKey="value"
                                    nameKey="name"
                                    cx="50%"
                                    cy="50%"
                                    outerRadius={90}
                                    label={({ name, percent }) =>
                                        `${name} ${((percent ?? 0) * 100).toFixed(0)}%`
                                    }
                                >
                                    {pieData.map((_, i) => (
                                        <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                                    ))}
                                </Pie>
                                <Tooltip />
                                <Legend wrapperStyle={{ fontSize: 11 }} />
                            </PieChart>
                        </ResponsiveContainer>
                    )}
                </Card>
            </Col>
        </Row>
    );
}
