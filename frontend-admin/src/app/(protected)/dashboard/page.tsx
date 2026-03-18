'use client';

import React, { useState } from 'react';
import { Card, Col, Row, Statistic, DatePicker, Select, Table, Spin, Tabs } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined, DollarOutlined, ShoppingOutlined, UserOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    useGetApiReportsSales,
    useGetApiReportsProducts,
    useGetApiReportsPayments,
    useGetApiReportsCustomers
} from '@/api/generated/reports/reports';
import dayjs from 'dayjs';

const { RangePicker } = DatePicker;
const { Option } = Select;

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

    if (loading) {
        return (
            <div style={{ textAlign: 'center', padding: 50 }}>
                <Spin size="large" />
            </div>
        );
    }

    return (
        <div style={{ paddingBottom: 24 }}>
            <AdminPageHeader
                title="Dashboard"
                breadcrumbs={[{ title: 'Home' }, { title: 'Dashboard' }]}
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
            />

            {/* Key Metrics Row */}
            <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Total Revenue"
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
                            title="Total Sales Count"
                            value={salesReport?.totalInvoices}
                            prefix={<ShoppingOutlined />}
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Average Sale"
                            value={salesReport?.averageOrderValue}
                            precision={2}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col span={6}>
                    <Card bordered={false}>
                        <Statistic
                            title="Active Customers"
                            value={customersReport?.totalCustomers}
                            prefix={<UserOutlined />}
                        />
                    </Card>
                </Col>
            </Row>

            <Row gutter={16}>
                {/* Top Selling Products */}
                <Col span={12}>
                    <Card title="Top Selling Products" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(productsReport?.topSellingProducts ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="productId"
                            columns={[
                                { title: 'Product', dataIndex: 'productName' },
                                { title: 'Quantity', dataIndex: 'quantitySold' },
                                { title: 'Revenue', dataIndex: 'revenue', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>

                {/* Sales by Payment Method */}
                <Col span={12}>
                    <Card title="Payment Methods" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={paymentsReport?.paymentsByMethod || []}
                            pagination={false}
                            rowKey="method"
                            columns={[
                                { title: 'Method', dataIndex: 'method' },
                                { title: 'Count', dataIndex: 'count' },
                                { title: 'Total', dataIndex: 'total', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>

            <div style={{ height: 24 }} />

            <Row gutter={16}>
                {/* Top Customers */}
                <Col span={12}>
                    <Card title="Top Customers" bordered={false} style={{ height: '100%' }}>
                        <Table
                            dataSource={(customersReport?.topCustomers ?? []).slice(0, 5)}
                            pagination={false}
                            rowKey="customerId"
                            columns={[
                                { title: 'Customer', dataIndex: 'customerName' },
                                { title: 'Orders', dataIndex: 'orderCount' },
                                { title: 'Spent', dataIndex: 'totalSpent', render: (val) => `€${Number(val ?? 0).toFixed(2)}` },
                            ]}
                        />
                    </Card>
                </Col>
            </Row>

        </div>
    );
}
