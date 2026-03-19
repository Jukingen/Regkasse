'use client';

import React, { useMemo, useState } from 'react';
import {
    Alert,
    Card,
    Col,
    DatePicker,
    Row,
    Spin,
    Statistic,
    Table,
    Tag,
    Space,
    Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';

const { RangePicker } = DatePicker;

const BASE = '/api/admin/offline-intent-coverage';

type OfflineIntentCoverageByRegisterDto = {
    cashRegisterId: string;
    total: number;
    withDeviceId: number;
    withSequence: number;
    deviceIdMissingRate: number; // 0..1
    sequenceMissingRate: number; // 0..1
    riskScore: number; // deviceIdMissingRate + sequenceMissingRate
};

type OfflineIntentCoverageResponse = {
    fromUtc: string;
    toUtc: string;
    total: number;
    withDeviceId: number;
    withSequence: number;
    deviceIdMissingRate: number;
    sequenceMissingRate: number;
    deviceIdCoveragePercent: number; // 0..100
    sequenceCoveragePercent: number; // 0..100
    lowCoverageAlert: boolean;
    alertReason?: string | null;
    byRegister: OfflineIntentCoverageByRegisterDto[];
};

type OfflineIntentCoverageTopRiskResponse = {
    fromUtc: string;
    toUtc: string;
    limit: number;
    registers: OfflineIntentCoverageByRegisterDto[];
};

function riskTagColor(score: number): string {
    // riskScore is 0..2 (missing rates sum). Use bands to make it actionable.
    if (score >= 1.0) return 'red';
    if (score >= 0.5) return 'orange';
    return 'green';
}

function formatPercent01AsPct(value: number): string {
    return `${(value * 100).toFixed(1)}%`;
}

export default function OfflineIntentCoveragePage() {
    const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
        dayjs().subtract(1, 'day'),
        dayjs(),
    ]);
    const [topLimit] = useState<number>(10);

    const params = useMemo(() => {
        return {
            fromUtc: dateRange[0].toISOString(),
            toUtc: dateRange[1].toISOString(),
        };
    }, [dateRange]);

    const coverageQuery = useQuery({
        queryKey: ['admin', 'offline-intent-coverage', 'summary', params],
        queryFn: () =>
            customInstance<OfflineIntentCoverageResponse>({
                url: BASE,
                method: 'GET',
                params,
            }),
        staleTime: 30_000,
    });

    const topRiskQuery = useQuery({
        queryKey: ['admin', 'offline-intent-coverage', 'top-risk', params, topLimit],
        queryFn: () =>
            customInstance<OfflineIntentCoverageTopRiskResponse>({
                url: `${BASE}/top-risk`,
                method: 'GET',
                params: { ...params, limit: topLimit },
            }),
        staleTime: 30_000,
    });

    const isLoading = coverageQuery.isLoading || topRiskQuery.isLoading;
    const error = coverageQuery.error ?? topRiskQuery.error;

    const byRegister = coverageQuery.data?.byRegister ?? [];
    const topRiskRegisters = topRiskQuery.data?.registers ?? [];

    const coverage = coverageQuery.data;

    const byRegisterColumns = [
        {
            title: 'Kasse',
            dataIndex: 'cashRegisterId',
            key: 'cashRegisterId',
            width: 140,
            render: (v: string) => (
                <Typography.Text code copyable>
                    {v?.slice(0, 8)}…
                </Typography.Text>
            ),
        },
        {
            title: 'Samples',
            dataIndex: 'total',
            key: 'total',
            width: 90,
            align: 'right' as const,
        },
        {
            title: 'DeviceId Missing',
            dataIndex: 'deviceIdMissingRate',
            key: 'deviceIdMissingRate',
            width: 170,
            render: (v: number) => formatPercent01AsPct(v),
        },
        {
            title: 'Sequence Missing',
            dataIndex: 'sequenceMissingRate',
            key: 'sequenceMissingRate',
            width: 170,
            render: (v: number) => formatPercent01AsPct(v),
        },
        {
            title: 'Risk Score',
            dataIndex: 'riskScore',
            key: 'riskScore',
            width: 120,
            render: (v: number) => (
                <Tag color={riskTagColor(v)}>
                    {v.toFixed(4)}
                </Tag>
            ),
        },
    ];

    return (
        <>
            <AdminPageHeader
                title="Offline Intent Coverage"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Offline Intent Coverage' },
                ]}
            />

            {isLoading ? (
                <div style={{ textAlign: 'center', padding: 80 }}>
                    <Spin size="large" />
                </div>
            ) : error ? (
                <Alert
                    type="error"
                    message="Coverage query failed"
                    description={error instanceof Error ? error.message : String(error)}
                    showIcon
                />
            ) : (
                <>
                    {coverage?.lowCoverageAlert ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 16 }}
                            message="Low offline-intent coverage"
                            description={coverage?.alertReason ?? 'Threshold exceeded.'}
                        />
                    ) : null}

                    <Card size="small" style={{ marginBottom: 16 }}>
                        <Space direction="horizontal" wrap>
                            <Typography.Text strong>Date Range (UTC):</Typography.Text>
                            <RangePicker
                                showTime
                                value={[dateRange[0], dateRange[1]]}
                                onChange={(dates) => {
                                    const d0 = dates?.[0];
                                    const d1 = dates?.[1];
                                    if (d0 && d1) setDateRange([d0, d1]);
                                }}
                            />
                        </Space>
                    </Card>

                    <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
                        <Col xs={24} md={6}>
                            <Card size="small">
                                <Statistic title="Samples" value={coverage?.total ?? 0} />
                            </Card>
                        </Col>
                        <Col xs={24} md={6}>
                            <Card size="small">
                                <Statistic
                                    title="DeviceId Coverage"
                                    value={(coverage?.deviceIdCoveragePercent ?? 0).toFixed(1) + '%'}
                                />
                            </Card>
                        </Col>
                        <Col xs={24} md={6}>
                            <Card size="small">
                                <Statistic
                                    title="Sequence Coverage"
                                    value={(coverage?.sequenceCoveragePercent ?? 0).toFixed(1) + '%'}
                                />
                            </Card>
                        </Col>
                        <Col xs={24} md={6}>
                            <Card size="small">
                                <Statistic
                                    title="Missing (DeviceId/Sequence)"
                                    value={
                                        `${formatPercent01AsPct(coverage?.deviceIdMissingRate ?? 0)} / ${formatPercent01AsPct(
                                            coverage?.sequenceMissingRate ?? 0
                                        )}`
                                    }
                                />
                            </Card>
                        </Col>
                    </Row>

                    <Card
                        title="Risk by register"
                        size="small"
                        style={{ marginBottom: 16 }}
                    >
                        <Table
                            columns={byRegisterColumns}
                            dataSource={byRegister}
                            rowKey={(r) => r.cashRegisterId}
                            pagination={false}
                            locale={{ emptyText: 'No coverage samples in this window.' }}
                            size="small"
                        />
                    </Card>

                    <Card title={`Top risk registers (limit ${topRiskQuery.data?.limit ?? topLimit})`} size="small">
                        <Table
                            columns={byRegisterColumns}
                            dataSource={topRiskRegisters}
                            rowKey={(r) => r.cashRegisterId}
                            pagination={false}
                            locale={{ emptyText: 'No top-risk samples in this window.' }}
                            size="small"
                        />
                    </Card>
                </>
            )}
        </>
    );
}

