'use client';

import React from 'react';
import { Card, Descriptions, Spin, Alert } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiTseStatus } from '@/api/generated/tse/tse';
import { useGetApiTseDevices } from '@/api/generated/tse/tse';

export default function RksvCmcCertificatePage() {
    const { data: tseStatus, isLoading: statusLoading, error: statusError } = useGetApiTseStatus();
    const { data: devices, isLoading: devicesLoading } = useGetApiTseDevices();

    const isLoading = statusLoading || devicesLoading;

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
                title="CMC / Certificate"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'CMC / Certificate' },
                ]}
            />

            {statusError && (
                <Alert type="error" message="TSE data unavailable" description={String(statusError)} style={{ marginBottom: 16 }} />
            )}

            <Card title="Certificate Status">
                <Descriptions column={1} bordered size="small">
                    <Descriptions.Item label="Certificate Status">{tseStatus?.certificateStatus ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="Serial Number">{tseStatus?.serialNumber ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="Kassen-ID">{tseStatus?.kassenId ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="Memory Status">{tseStatus?.memoryStatus ?? '—'}</Descriptions.Item>
                    <Descriptions.Item label="Last Signature Time">{tseStatus?.lastSignatureTime ?? '—'}</Descriptions.Item>
                </Descriptions>
            </Card>

            <Card title="Available TSE Devices" style={{ marginTop: 16 }}>
                {devices && devices.length > 0 ? (
                    <Descriptions column={1} bordered size="small">
                        {devices.map((d, i) => (
                            <Descriptions.Item key={d.id ?? i} label={d.serialNumber || `Device ${i + 1}`}>
                                {d.kassenId ?? d.serialNumber ?? d.id ?? '—'}
                            </Descriptions.Item>
                        ))}
                    </Descriptions>
                ) : (
                    <p>No TSE devices detected.</p>
                )}
            </Card>
        </>
    );
}
