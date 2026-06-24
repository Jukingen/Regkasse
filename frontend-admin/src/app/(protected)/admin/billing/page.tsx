'use client';

import React, { useEffect } from 'react';
import { Card, Col, Row, Statistic, Space, Button, Table, Tag, Spin } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    ShoppingOutlined,
    CheckCircleOutlined,
    CloseCircleOutlined,
    ClockCircleOutlined,
    PlusOutlined,
} from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useBillingStats, useExpiringLicenses } from '@/features/billing/hooks';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { formatDate } from '@/lib/dateFormatter';
import type { ExpiringLicenseInfo } from '@/api/generated/model';

export default function BillingOverviewPage() {
    const router = useRouter();
    const { message } = useAntdApp();

    const { data: stats, isLoading: statsLoading, error: statsError } = useBillingStats();
    const { data: expiring, isLoading: expiringLoading } = useExpiringLicenses(30);

    useEffect(() => {
        if (statsError) {
            message.error('Statistiken konnten nicht geladen werden');
        }
    }, [statsError, message]);

    const expiringColumns: ColumnsType<ExpiringLicenseInfo> = [
        {
            title: 'Mandant',
            dataIndex: 'tenantName',
            key: 'tenantName',
            render: (text: string | null | undefined, record) => (
                <Button type="link" style={{ padding: 0 }} onClick={() => router.push(`/admin/tenants/${record.tenantId}`)}>
                    {text ?? record.tenantSlug ?? '—'}
                </Button>
            ),
        },
        { title: 'Slug', dataIndex: 'tenantSlug', key: 'tenantSlug' },
        { title: 'Lizenzschlüssel', dataIndex: 'licenseKey', key: 'licenseKey', ellipsis: true },
        {
            title: 'Gültig bis',
            dataIndex: 'validUntilUtc',
            key: 'validUntilUtc',
            render: (date: string | undefined) => (date ? formatDate(date) : '—'),
        },
        {
            title: 'Tage verbleibend',
            dataIndex: 'daysRemaining',
            key: 'daysRemaining',
            render: (days: number | undefined) => {
                const n = days ?? 0;
                return (
                    <Tag color={n <= 7 ? 'red' : n <= 15 ? 'orange' : 'gold'}>
                        {n} Tage
                    </Tag>
                );
            },
        },
    ];

    const expiringSoon = stats?.expiringSoonLicenses ?? 0;

    return (
        <BillingAccessGate>
            <div style={{ padding: 24 }}>
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16 }}>
                        <div>
                            <h1 style={{ margin: 0 }}>Lizenzverkauf</h1>
                            <p style={{ color: '#64748b', marginBottom: 0 }}>
                                Verwalten Sie Lizenzverkäufe und -verlängerungen für alle Mandanten.
                            </p>
                        </div>
                        <Button
                            type="primary"
                            icon={<PlusOutlined />}
                            onClick={() => router.push('/admin/billing/sales/new')}
                        >
                            Neuer Verkauf
                        </Button>
                    </div>

                    <Spin spinning={statsLoading}>
                        <Row gutter={[16, 16]}>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Gesamtumsatz (Netto)"
                                        value={stats?.totalRevenueNet ?? 0}
                                        precision={2}
                                        prefix="€"
                                        styles={{ content: { color: '#1a56db' } }}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Gesamtumsatz (Brutto)"
                                        value={stats?.totalRevenueGross ?? 0}
                                        precision={2}
                                        prefix="€"
                                        styles={{ content: { color: '#16a34a' } }}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Aktive Lizenzen"
                                        value={stats?.activeLicenses ?? 0}
                                        prefix={<CheckCircleOutlined style={{ color: '#16a34a' }} />}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Bald ablaufend (≤30 Tage)"
                                        value={expiringSoon}
                                        prefix={<ClockCircleOutlined style={{ color: '#eab308' }} />}
                                        styles={{ content: { color: expiringSoon > 5 ? '#dc2626' : '#eab308' } }}
                                    />
                                </Card>
                            </Col>
                        </Row>

                        <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Verkäufe gesamt"
                                        value={stats?.totalSales ?? 0}
                                        prefix={<ShoppingOutlined />}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Abgelaufene Lizenzen"
                                        value={stats?.expiredLicenses ?? 0}
                                        prefix={<CloseCircleOutlined style={{ color: '#dc2626' }} />}
                                        styles={{ content: { color: '#dc2626' } }}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Mandanten mit Lizenz"
                                        value={stats?.totalTenantsWithLicense ?? 0}
                                    />
                                </Card>
                            </Col>
                            <Col xs={24} sm={12} lg={6}>
                                <Card>
                                    <Statistic
                                        title="Durchschnittspreis (Netto)"
                                        value={stats?.averagePriceNet ?? 0}
                                        precision={2}
                                        prefix="€"
                                    />
                                </Card>
                            </Col>
                        </Row>
                    </Spin>

                    <Card
                        title="Bald ablaufende Lizenzen"
                        extra={
                            <Button type="link" onClick={() => router.push('/admin/billing/sales')}>
                                Alle anzeigen
                            </Button>
                        }
                    >
                        <Spin spinning={expiringLoading}>
                            <Table<ExpiringLicenseInfo>
                                dataSource={expiring ?? []}
                                columns={expiringColumns}
                                rowKey={(row) => row.licenseSaleId ?? `${row.tenantId}-${row.licenseKey}`}
                                pagination={false}
                                locale={{ emptyText: 'Keine bald ablaufenden Lizenzen' }}
                            />
                        </Spin>
                    </Card>
                </Space>
            </div>
        </BillingAccessGate>
    );
}
