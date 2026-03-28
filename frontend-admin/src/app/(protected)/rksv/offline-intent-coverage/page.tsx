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
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import {
    getApiAdminOfflineIntentCoverage,
    getApiAdminOfflineIntentCoverageTopRisk,
} from '@/api/generated/admin/admin';
import {
    getAdminCashRegisters,
} from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { CashRegisterRow } from '@/features/tagesabschluss/normalizers';
import type { OfflineIntentCoverageByRegisterDto } from '@/api/generated/model';

const { RangePicker } = DatePicker;

function riskTagColor(score: number): string {
    if (score >= 1.0) return 'red';
    if (score >= 0.5) return 'orange';
    return 'green';
}

function formatPercent01AsPct(value: number): string {
    return `${(value * 100).toFixed(1)}%`;
}

export default function OfflineIntentCoveragePage() {
    const { t } = useI18n();
    const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
        dayjs().subtract(1, 'day'),
        dayjs(),
    ]);
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
    const [topLimit, setTopLimit] = useState<number>(10);

    const registersQuery = useQuery({
        queryKey: rksvAdminQueryKeys.cashRegisters,
        queryFn: getAdminCashRegisters,
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
        queryKey: rksvAdminQueryKeys.offlineIntentCoverage.summary(params),
        queryFn: () => getApiAdminOfflineIntentCoverage(params),
        staleTime: 30_000,
    });

    const topRiskQuery = useQuery({
        queryKey: rksvAdminQueryKeys.offlineIntentCoverage.topRisk(topRiskParams),
        queryFn: () => getApiAdminOfflineIntentCoverageTopRisk(topRiskParams),
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
                    ADMIN_OVERVIEW_CRUMB,
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
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={error}
                            logContext="OfflineIntentCoverage.load"
                            fallbackKey="common.messages.unknownError"
                        />
                    }
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
                            rowKey={(r, idx) => r.cashRegisterId ?? `register-${idx ?? 0}`}
                            pagination={false}
                            locale={{ emptyText: 'No coverage samples in this window.' }}
                            size="small"
                        />
                    </Card>

                    <Card title={`Top risk registers (limit ${topRiskQuery.data?.limit ?? topLimit})`} size="small">
                        <Table
                            columns={byRegisterColumns}
                            dataSource={topRiskRegisters}
                            rowKey={(r, idx) => r.cashRegisterId ?? `top-risk-${idx ?? 0}`}
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
