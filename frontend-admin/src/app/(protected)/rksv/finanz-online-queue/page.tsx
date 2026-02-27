'use client';

import React from 'react';
import { Card, Table, Tag, Statistic, Row, Col, Spin } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiFinanzOnlineStatus } from '@/api/generated/finanz-online/finanz-online';
import { useGetApiFinanzOnlineErrors } from '@/api/generated/finanz-online/finanz-online';

export default function RksvFinanzOnlineQueuePage() {
    const { data: status, isLoading: statusLoading } = useGetApiFinanzOnlineStatus();
    const { data: errors, isLoading: errorsLoading } = useGetApiFinanzOnlineErrors();

    const isLoading = statusLoading || errorsLoading;

    if (isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 80 }}>
                <Spin size="large" />
            </div>
        );
    }

    const errorColumns = [
        { title: 'Code', dataIndex: 'code', key: 'code', width: 120 },
        { title: 'Message', dataIndex: 'message', key: 'message', ellipsis: true },
        { title: 'Invoice', dataIndex: 'invoiceNumber', key: 'invoiceNumber' },
        { title: 'Timestamp', dataIndex: 'timestamp', key: 'timestamp' },
    ];

    return (
        <>
            <AdminPageHeader
                title="FinanzOnline Queue"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'FinanzOnline Queue' },
                ]}
            />

            <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic title="Pending Invoices" value={status?.pendingInvoices ?? 0} />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic title="Pending Reports" value={status?.pendingReports ?? 0} />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic title="Connection" value={status?.isConnected ? 'Connected' : 'Offline'} />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card size="small">
                        <Statistic title="Last Sync" value={status?.lastSync ? new Date(status.lastSync).toLocaleString() : '—'} />
                    </Card>
                </Col>
            </Row>

            <Card title="Queue Errors">
                {errors && errors.length > 0 ? (
                    <Table
                        columns={errorColumns}
                        dataSource={errors}
                        rowKey={(r, i) => (r as { invoiceNumber?: string; code?: string }).invoiceNumber ?? (r as { code?: string }).code ?? String(i)}
                        pagination={false}
                        size="small"
                    />
                ) : (
                    <p>No errors in queue.</p>
                )}
            </Card>
        </>
    );
}
