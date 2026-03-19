'use client';

import React, { useState } from 'react';
import { Card, Form, Input, Button, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';

/**
 * Replay-Batch-Suche: Correlation-ID eingeben und Batch-Detail anzeigen (Incident-Debugging).
 */
export default function ReplayBatchSearchPage() {
    const router = useRouter();
    const [loading, setLoading] = useState(false);
    const [form] = Form.useForm();

    const onFinish = (values: { correlationId: string }) => {
        const id = values.correlationId?.trim();
        if (!id) return;
        setLoading(true);
        router.push(`/rksv/replay-batch/${encodeURIComponent(id)}`);
        setLoading(false);
    };

    return (
        <>
            <AdminPageHeader
                title="Replay-Batch (Correlation-ID)"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Replay-Batch' },
                ]}
            />
            <Card>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                    Replay-Batch-Correlation-ID eingeben (z. B. aus Logs oder Replay-Response), um Batch-Details,
                    Success/Fail/Duplicate und Log-Trace-Link anzuzeigen.
                </Typography.Paragraph>
                <Form form={form} layout="inline" onFinish={onFinish}>
                    <Form.Item
                        name="correlationId"
                        label="Correlation-ID (Guid)"
                        rules={[{ required: true, message: 'Correlation-ID eingeben' }]}
                        style={{ minWidth: 320 }}
                    >
                        <Input placeholder="z. B. a1b2c3d4-e5f6-7890-abcd-ef1234567890" allowClear />
                    </Form.Item>
                    <Form.Item>
                        <Button type="primary" htmlType="submit" loading={loading}>
                            Anzeigen
                        </Button>
                    </Form.Item>
                </Form>
            </Card>
        </>
    );
}
