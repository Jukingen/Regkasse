'use client';

import React from 'react';
import { Table, Card, Typography, Tag, Space, Button } from 'antd';
import { CreditCardOutlined, InfoCircleOutlined } from '@ant-design/icons';
import { useGetApiPayment } from '@/api/generated/payment/payment';
import type { PaymentResponse } from '@/api/generated/model';
import dayjs from 'dayjs';

const { Title } = Typography;

export default function PaymentsPage() {
    const { data: payments, isLoading } = useGetApiPayment();

    const columns = [
        {
            title: 'Transaction ID',
            dataIndex: 'transactionId',
            key: 'transactionId',
            render: (text: string) => <code style={{ fontSize: '12px' }}>{text}</code>,
        },
        {
            title: 'Date',
            dataIndex: 'createdAt',
            key: 'createdAt',
            render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
        },
        {
            title: 'Amount',
            dataIndex: 'amount',
            key: 'amount',
            align: 'right' as const,
            render: (val: number, record: any) => `${val.toFixed(2)} ${record.currency || 'EUR'}`,
        },
        {
            title: 'Method',
            dataIndex: 'method',
            key: 'method',
            render: (method: string) => <Tag color="blue">{method}</Tag>,
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => {
                const colors: Record<string, string> = {
                    'Success': 'green',
                    'Pending': 'orange',
                    'Failed': 'red',
                };
                return <Tag color={colors[status] || 'default'}>{status}</Tag>;
            },
        },
        {
            title: 'Actions',
            key: 'actions',
            render: () => (
                <Button size="small" icon={<InfoCircleOutlined />}>Details</Button>
            ),
        },
    ];

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <Title level={3} style={{ margin: 0 }}>Payments</Title>
                <Space>
                    <Button icon={<CreditCardOutlined />}>Terminal Status</Button>
                </Space>
            </div>

            <Table
                columns={columns}
                dataSource={payments || []}
                loading={isLoading}
                rowKey="id"
            />
        </Card>
    );
}
