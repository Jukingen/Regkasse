'use client';

import React from 'react';
import { Card, Row, Col, Statistic, Tag, Spin, Alert } from 'antd';
import { CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiTseStatus } from '@/api/generated/tse/tse';
import { useGetApiFinanzOnlineStatus } from '@/api/generated/finanz-online/finanz-online';

export default function RksvStatusPage() {
    const { data: tseStatus, isLoading: tseLoading, error: tseError } = useGetApiTseStatus();
    const { data: foStatus, isLoading: foLoading, error: foError } = useGetApiFinanzOnlineStatus();

    const isLoading = tseLoading || foLoading;

    if (isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 80 }}>
                <Spin size="large" />
            </div>
        );
    }

    return (
        <>
            <AdminPageHeader
                title="RKSV General Status"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'General Status' },
                ]}
            />

            {tseError && (
                <Alert type="error" message="TSE status unavailable" description={String(tseError)} style={{ marginBottom: 16 }} />
            )}
            {foError && (
                <Alert type="warning" message="FinanzOnline status unavailable" style={{ marginBottom: 16 }} />
            )}

            <Row gutter={[16, 16]}>
                <Col xs={24} md={12}>
                    <Card title="TSE (Technical Security Element)" size="small">
                        <Statistic
                            title="Connection"
                            value={tseStatus?.isConnected ? 'Connected' : 'Disconnected'}
                            valueRender={() => (
                                <Tag color={tseStatus?.isConnected ? 'green' : 'red'} icon={tseStatus?.isConnected ? <CheckCircleOutlined /> : <CloseCircleOutlined />}>
                                    {tseStatus?.isConnected ? 'Connected' : 'Disconnected'}
                                </Tag>
                            )}
                        />
                        {tseStatus?.serialNumber && <p style={{ marginTop: 8 }}>Serial: {tseStatus.serialNumber}</p>}
                        {tseStatus?.kassenId && <p>Kassen-ID: {tseStatus.kassenId}</p>}
                        {tseStatus?.certificateStatus && <p>Certificate: {tseStatus.certificateStatus}</p>}
                        {tseStatus?.canCreateInvoices !== undefined && (
                            <p>Can create invoices: {tseStatus.canCreateInvoices ? 'Yes' : 'No'}</p>
                        )}
                    </Card>
                </Col>

                <Col xs={24} md={12}>
                    <Card title="FinanzOnline" size="small">
                        <Statistic
                            title="Status"
                            value={foStatus?.isConnected ? 'Connected' : 'Not connected'}
                            valueRender={() => (
                                <Tag color={foStatus?.isConnected ? 'green' : 'orange'} icon={foStatus?.isConnected ? <CheckCircleOutlined /> : <CloseCircleOutlined />}>
                                    {foStatus?.isConnected ? 'Connected' : 'Not connected'}
                                </Tag>
                            )}
                        />
                        {foStatus?.pendingInvoices !== undefined && <p style={{ marginTop: 8 }}>Pending invoices: {foStatus.pendingInvoices}</p>}
                        {foStatus?.lastSync && <p>Last sync: {foStatus.lastSync}</p>}
                    </Card>
                </Col>
            </Row>
        </>
    );
}
