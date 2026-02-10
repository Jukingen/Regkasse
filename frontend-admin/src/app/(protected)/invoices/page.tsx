'use client';

import React, { useState } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Input, DatePicker } from 'antd';
import { SearchOutlined, EyeOutlined, PrinterOutlined } from '@ant-design/icons';
import { useGetApiInvoice } from '@/api/generated/invoice/invoice';
import type { Invoice } from '@/api/generated/model';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

export default function InvoicesPage() {
    const [searchText, setSearchText] = useState('');
    const { data: invoices, isLoading } = useGetApiInvoice();

    const columns = [
        {
            title: 'Invoice No',
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            render: (text: string) => <span style={{ fontWeight: 'bold' }}>{text}</span>,
        },
        {
            title: 'Date',
            dataIndex: 'date',
            key: 'date',
            render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
            sorter: (a: Invoice, b: Invoice) => dayjs(a.date).unix() - dayjs(b.date).unix(),
        },
        {
            title: 'Customer',
            dataIndex: 'customerName',
            key: 'customerName',
            render: (text: string) => text || '-',
        },
        {
            title: 'Total',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            align: 'right' as const,
            render: (amount: number) => `€${amount.toFixed(2)}`,
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => {
                const colors: Record<string, string> = {
                    'Paid': 'green',
                    'Draft': 'blue',
                    'Cancelled': 'red',
                };
                return <Tag color={colors[status] || 'default'}>{status.toUpperCase()}</Tag>;
            },
        },
        {
            title: 'Actions',
            key: 'actions',
            render: (_: any, record: Invoice) => (
                <Space>
                    <Button size="small" icon={<EyeOutlined />} title="View" />
                    <Button size="small" icon={<PrinterOutlined />} title="Print" />
                </Space>
            ),
        },
    ];

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <Title level={3} style={{ margin: 0 }}>Invoices</Title>
                <Space>
                    <Input
                        placeholder="Search invoices..."
                        prefix={<SearchOutlined />}
                        onChange={e => setSearchText(e.target.value)}
                        style={{ width: 250 }}
                    />
                    <RangePicker />
                    <Button type="primary">Export CSV</Button>
                </Space>
            </div>

            <Table
                columns={columns}
                dataSource={invoices}
                loading={isLoading}
                rowKey="id"
                pagination={{
                    showSizeChanger: true,
                    defaultPageSize: 10,
                }}
            />
        </Card>
    );
}
