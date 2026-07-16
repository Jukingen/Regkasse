'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Button, Card, Col, Row, Space, Spin, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, EyeOutlined, PlusOutlined } from '@ant-design/icons';
import type { LicenseSaleResponse } from '@/api/generated/model';
import { useI18n, formatGermanDateTime } from '@/i18n';
import { useBillingSalesList, useBillingStats } from '@/features/billing/hooks';

function saleStatusColor(status: string | null | undefined): string {
    switch (status) {
        case 'active':
            return 'green';
        case 'cancelled':
            return 'red';
        case 'refunded':
            return 'orange';
        default:
            return 'default';
    }
}

function formatTenantSaleStatus(
    status: string | null | undefined,
    t: (key: string) => string,
): string {
    switch (status) {
        case 'active':
            return t('license.tenant.status.active');
        case 'cancelled':
            return t('license.tenant.status.cancelled');
        case 'refunded':
            return t('license.tenant.status.refunded');
        default:
            return status ?? '—';
    }
}

function daysRemainingTagColor(days: number): string {
    if (days <= 7) return 'red';
    if (days <= 30) return 'orange';
    return 'green';
}

function computeDaysRemaining(validUntilUtc: string | null | undefined): number | null {
    if (!validUntilUtc) return null;
    const diffMs = new Date(validUntilUtc).getTime() - Date.now();
    return Math.ceil(diffMs / (1000 * 60 * 60 * 24));
}

export function TenantLicenseBillingTab() {
    const router = useRouter();
    const { t, formatLocale } = useI18n();

    const { data: salesData, isLoading: salesLoading } = useBillingSalesList({
        page: 1,
        pageSize: 100,
        status: 'all',
    });
    const { data: stats, isLoading: statsLoading } = useBillingStats();

    const columns = useMemo<ColumnsType<LicenseSaleResponse>>(
        () => [
            {
                title: t('license.superAdmin.table.tenant'),
                dataIndex: 'tenantName',
                key: 'tenantName',
                render: (name: string | null | undefined, record) =>
                    record.tenantId ? (
                        <Link href={`/admin/tenants/${record.tenantId}`}>{name ?? record.tenantSlug ?? '—'}</Link>
                    ) : (
                        name ?? '—'
                    ),
            },
            {
                title: t('license.superAdmin.table.slug'),
                dataIndex: 'tenantSlug',
                key: 'tenantSlug',
                render: (slug: string | null | undefined) =>
                    slug ? (
                        <Typography.Text code type="secondary">
                            {slug}
                        </Typography.Text>
                    ) : (
                        '—'
                    ),
            },
            {
                title: t('license.superAdmin.table.licenseKey'),
                dataIndex: 'licenseKey',
                key: 'licenseKey',
                ellipsis: true,
                render: (key: string | null | undefined) =>
                    key ? (
                        <Typography.Text code style={{ fontSize: 12 }}>
                            {key}
                        </Typography.Text>
                    ) : (
                        '—'
                    ),
            },
            {
                title: t('license.superAdmin.table.validUntil'),
                dataIndex: 'validUntilUtc',
                key: 'validUntilUtc',
                width: 200,
                render: (date: string | undefined) => {
                    if (!date) return '—';
                    const days = computeDaysRemaining(date);
                    return (
                        <Space size="small">
                            <span>{formatGermanDateTime(date)}</span>
                            {days != null ? (
                                <Tag color={daysRemainingTagColor(days)}>
                                    {t('license.tenant.daysRemaining', { count: days })}
                                </Tag>
                            ) : null}
                        </Space>
                    );
                },
            },
            {
                title: t('license.superAdmin.table.status'),
                dataIndex: 'status',
                key: 'status',
                width: 130,
                render: (status: string | null | undefined) => (
                    <Tag color={saleStatusColor(status)}>{formatTenantSaleStatus(status, t)}</Tag>
                ),
            },
            {
                title: t('license.superAdmin.table.actions'),
                key: 'actions',
                width: 100,
                fixed: 'right',
                render: (_, record) => (
                    <Space size="small">
                        {record.id ? (
                            <Button
                                type="link"
                                size="small"
                                icon={<EyeOutlined />}
                                aria-label={t('billing.sales.view')}
                                onClick={() => router.push(`/admin/billing/sales/${record.id}`)}
                            />
                        ) : null}
                        {record.tenantId ? (
                            <Button
                                type="link"
                                size="small"
                                icon={<EditOutlined />}
                                aria-label={t('license.superAdmin.table.edit')}
                                onClick={() => router.push(`/admin/tenants/${record.tenantId}`)}
                            />
                        ) : null}
                    </Space>
                ),
            },
        ],
        [formatLocale, router, t],
    );

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Spin spinning={statsLoading}>
                <Row gutter={[16, 16]}>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small" variant="borderless">
                            <Statistic title={t('license.tenant.active')} value={stats?.activeLicenses ?? 0} />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small" variant="borderless">
                            <Statistic
                                title={t('license.tenant.expiringSoon')}
                                value={stats?.expiringSoonLicenses ?? 0}
                                styles={{ content: { color: '#eab308' } }}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small" variant="borderless">
                            <Statistic
                                title={t('license.tenant.expired')}
                                value={stats?.expiredLicenses ?? 0}
                                styles={{ content: { color: '#dc2626' } }}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small" variant="borderless">
                            <Statistic
                                title={t('license.tenant.tenantsWithLicense')}
                                value={stats?.totalTenantsWithLicense ?? 0}
                            />
                        </Card>
                    </Col>
                </Row>
            </Spin>

            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexWrap: 'wrap',
                    gap: 16,
                }}
            >
                <div>
                    <Typography.Title level={5} style={{ margin: 0 }}>
                        {t('license.tenant.title')}
                    </Typography.Title>
                    <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
                        {t('license.tenant.subtitle')}
                    </Typography.Paragraph>
                </div>
                <Space wrap>
                    <Button
                        type="primary"
                        icon={<PlusOutlined />}
                        onClick={() => router.push('/admin/billing/sales/new')}
                    >
                        {t('license.tenant.newSale')}
                    </Button>
                    <Button onClick={() => router.push('/admin/billing/sales')}>
                        {t('license.tenant.allSales')}
                    </Button>
                </Space>
            </div>

            <Spin spinning={salesLoading}>
                <Table<LicenseSaleResponse>
                    rowKey={(row) => row.id ?? `${row.tenantId}-${row.licenseKey}`}
                    columns={columns}
                    dataSource={salesData?.items ?? []}
                    pagination={false}
                    scroll={{ x: 960 }}
                    locale={{ emptyText: t('license.tenant.noResults') }}
                />
            </Spin>
        </Space>
    );
}
