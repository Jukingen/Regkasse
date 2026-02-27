'use client';

import React from 'react';
import { Card, Table, Tag, Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLog } from '@/api/generated/model';
import dayjs from 'dayjs';

export default function RksvVerificationsPage() {
    const { data, isLoading } = useGetApiAuditLog({ page: 1, pageSize: 100 });

    const signatureEntries =
        data?.auditLogs?.filter(
            (e: AuditLog) =>
                e.action?.toLowerCase().includes('signature') ||
                e.entityType?.toLowerCase().includes('receipt') ||
                e.entityType?.toLowerCase().includes('payment')
        ) ?? [];

    const columns = [
        {
            title: 'Timestamp',
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 180,
            render: (ts: string) => dayjs(ts).format('DD.MM.YYYY HH:mm:ss'),
        },
        {
            title: 'User',
            dataIndex: ['user', 'userName'],
            key: 'userName',
            render: (_: unknown, r: AuditLog) => r.user?.userName ?? r.userId ?? '—',
        },
        {
            title: 'Action',
            dataIndex: 'action',
            key: 'action',
            render: (a: string) => <Tag color="blue">{a}</Tag>,
        },
        {
            title: 'Entity',
            dataIndex: 'entityType',
            key: 'entityType',
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (s: string) => <Tag color={s === 'Success' ? 'green' : 'red'}>{s ?? '—'}</Tag>,
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
        },
    ];

    return (
        <>
            <AdminPageHeader
                title="Last 100 Verification Results"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Verifications' },
                ]}
            />

            <Card>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                    Audit log entries related to signatures and payments (max 100). Dedicated signature verification history endpoint may be added later.
                </Typography.Paragraph>
                <Table
                    columns={columns}
                    dataSource={signatureEntries}
                    loading={isLoading}
                    rowKey={(r) => r.id ?? r.timestamp ?? r.createdAt ?? ''}
                    pagination={false}
                    size="small"
                />
            </Card>
        </>
    );
}
