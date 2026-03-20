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
    Select,
    InputNumber,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { normalizeCashRegisterListBody, type CashRegisterRow } from '@/features/tagesabschluss/normalizers';

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
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
    const [topLimit, setTopLimit] = useState<number>(10);

    const registersQuery = useQuery({
        queryKey: ['admin', 'cash-registers', 'offline-coverage-filter'],
        queryFn: async () => {
            const raw = await getApiCashRegister();
            return normalizeCashRegisterListBody(raw);
        },
        staleTime: 60_000,
    });

    const registerOptions: { value: string; label: string }[] = useMemo(() => {
        const rows = registersQuery.data ?? [];
        return rows
            .filter((r: CashRegisterRow) => typeof r.id === 'string' && r.id.length > 0)
            .map((r) => ({
                value: r.id as string,
                label: r.registerNumber ? `${r.registerNumber} (${(r.id as string).slice(0, 8)}…)` : (r.id as string),
            }));
    }, [registersQuery.data]);

    const params = useMemo(() => {
        const p: Record<string, string> = {
            fromUtc: dateRange[0].toISOString(),
            toUtc: dateRange[1].toISOString(),
        };
        const cr = cashRegisterId?.trim();
        if (cr) p.cashRegisterId = cr;
        return p;
    }, [dateRange, cashRegisterId]);

    const topRiskParams = useMemo(() => {
        const lim = Math.min(100, Math.max(1, Math.floor(topLimit) || 10));
        return { ...params, limit: lim };
    }, [params, topLimit]);

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
        queryKey: ['admin', 'offline-intent-coverage', 'top-risk', topRiskParams],
        queryFn: () =>
            customInstance<OfflineIntentCoverageTopRiskResponse>({
                url: `${BASE}/top-risk`,
                method: 'GET',
                params: topRiskParams,
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
                        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                            <Space wrap align="center">
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
                            <Space wrap align="center">
                                <Typography.Text strong>Kasse (optional):</Typography.Text>
                                <Select
                                    allowClear
                                    placeholder="Alle Kassen"
                                    style={{ minWidth: 260 }}
                                    value={cashRegisterId}
                                    onChange={(v) => setCashRegisterId(v)}
                                    options={registerOptions}
                                    loading={registersQuery.isLoading}
                                />
                            </Space>
                            <Space wrap align="center">
                                <Typography.Text strong>Top-Risk Limit:</Typography.Text>
                                <InputNumber
                                    min={1}
                                    max={100}
                                    value={topLimit}
                                    onChange={(v) => setTopLimit(typeof v === 'number' ? v : 10)}
                                />
                                <Typography.Text type="secondary">(1–100, Standard 10)</Typography.Text>
                            </Space>
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
