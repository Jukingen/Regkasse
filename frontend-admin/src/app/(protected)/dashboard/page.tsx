'use client';

import React, { useState } from 'react';
import { Card, Col, Row, Statistic, DatePicker, Table, Spin, Typography } from 'antd';
import { DollarOutlined, ShoppingOutlined, UserOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import {
    useGetApiReportsSales,
    useGetApiReportsProducts,
    useGetApiReportsPayments,
    useGetApiReportsCustomers
} from '@/api/generated/reports/reports';
import dayjs from 'dayjs';

const { RangePicker } = DatePicker;

export default function DashboardPage() {
    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().startOf('month'),
        dayjs().endOf('month')
    ]);

    const startDate = dateRange[0].format('YYYY-MM-DD');
    const endDate = dateRange[1].format('YYYY-MM-DD');

    // 1. Sales Report
    const { data: salesReport, isLoading: loadingSales } = useGetApiReportsSales({
        startDate,
        endDate
    });

    // 2. Products Report
    const { data: productsReport } = useGetApiReportsProducts({
        startDate,
        endDate,
    });

    // 3. Payment Methods
    const { data: paymentsReport } = useGetApiReportsPayments({
        startDate,
        endDate
    });

    // 4. Customer Stats
    const { data: customersReport } = useGetApiReportsCustomers({
        startDate,
        endDate,
    });

    const loading = loadingSales;

    return (
        <div style={{ paddingBottom: 24 }}>
            <AdminPageHeader
                title="Übersicht"
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB]}
                actions={
                    <RangePicker
                        value={dateRange}
                        onChange={(dates) => {
                            if (dates && dates[0] && dates[1]) {
                                setDateRange([dates[0], dates[1]]);
                            }
                        }}
                    />
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Geschäftszahlen für den gewählten Zeitraum (Umsatz, Verteilung, Top-Artikel). Operative RKSV-Einstiege:
                    Seitenleiste unter «RKSV».
                </Typography.Paragraph>
            </AdminPageHeader>

            {loading ? (
                <div
                    role="status"
                    aria-live="polite"
                    aria-busy="true"
                    aria-label="Berichte werden geladen"
                    style={{ textAlign: 'center', padding: 50 }}
                >
                    <Spin size="large" tip="Berichte werden geladen…" />
                </div>
            ) : (
                <>
            {/* Key Metrics Row */}
            <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Gesamtumsatz"
                            value={salesReport?.totalSales}
                            precision={2}
                            valueStyle={{ color: '#3f8600' }}
                            prefix={<DollarOutlined />}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Anzahl Verkäufe"
                            value={salesReport?.totalInvoices}
                            prefix={<ShoppingOutlined />}
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Ø Verkauf"
                            value={salesReport?.averageOrderValue}
                            precision={2}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Aktive Kunden"
                            value={customersReport?.totalCustomers}
                            prefix={<UserOutlined />}
                        />
                    </Card>
                </Col>
            </Row>

            <Row gutter={16}>
                {/* Top Selling Products */}
                <Col span={12}>
                    <Card title="Meistverkaufte Produkte" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(productsReport?.topSellingProducts ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="productId"
                            columns={[
                                { title: 'Produkt', dataIndex: 'productName' },
                                { title: 'Menge', dataIndex: 'quantitySold' },
                                { title: 'Umsatz', dataIndex: 'revenue', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>

                {/* Sales by Payment Method */}
                <Col span={12}>
                    <Card title="Zahlungsarten" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={paymentsReport?.paymentsByMethod || []}
                            pagination={false}
                            rowKey="method"
                            columns={[
                                { title: 'Zahlungsart', dataIndex: 'method' },
                                { title: 'Anzahl', dataIndex: 'count' },
                                { title: 'Summe', dataIndex: 'total', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>

            <div style={{ height: 24 }} />

            <Row gutter={16}>
                {/* Top Customers */}
                <Col span={12}>
                    <Card title="Top-Kunden" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(customersReport?.topCustomers ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="customerId"
                            columns={[
                                { title: 'Kunde', dataIndex: 'customerName' },
                                { title: 'Bestellungen', dataIndex: 'orderCount' },
                                { title: 'Ausgaben', dataIndex: 'totalSpent', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>
                </>
            )}
        </div>
    );
}
