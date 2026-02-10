'use client';

import React, { useState } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Select, DatePicker } from 'antd';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntry } from '@/api/generated/model';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

export default function AuditLogsPage() {
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);

    const { data, isLoading } = useGetApiAuditLog({
        page,
        pageSize,
    });

    const columns = [
        {
            title: 'Timestamp',
            dataIndex: 'timestamp',
            key: 'timestamp',
            render: (ts: string) => dayjs(ts).format('DD.MM.YYYY HH:mm:ss'),
        },
        {
            title: 'User',
            dataIndex: 'userName',
            key: 'userName',
        },
        {
            title: 'Action',
            dataIndex: 'action',
            key: 'action',
            render: (action: string) => <Tag color="blue">{action}</Tag>,
        },
        {
            title: 'Entity',
            dataIndex: 'entityType',
            key: 'entityType',
        },
        {
            title: 'Details',
            dataIndex: 'details',
            key: 'details',
            ellipsis: true,
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => (
                <Tag color={status === 'Success' ? 'green' : 'red'}>{status}</Tag>
            ),
        }
    ];

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <Title level={3} style={{ margin: 0 }}>Audit Logs</Title>
                <Space>
                    <Select placeholder="Action Filter" style={{ width: 150 }} allowClear>
                        <Select.Option value="Login">Login</Select.Option>
                        <Select.Option value="CreateInvoice">Create Invoice</Select.Option>
                        <Select.Option value="Payment">Payment</Select.Option>
                    </Select>
                    <RangePicker />
                    <Button>Export Logs</Button>
                </Space>
            </div>

            <Table
                columns={columns}
                dataSource={data?.items || []}
                loading={isLoading}
                rowKey="id"
                pagination={{
                    current: page,
                    pageSize: pageSize,
                    total: data?.totalCount,
                    onChange: (p, s) => {
                        setPage(p);
                        setPageSize(s);
                    },
                }}
            />
        </Card>
    );
}
