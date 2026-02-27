'use client';

import React from 'react';
import { Card, Alert, Spin, Tag, Typography, Collapse } from 'antd';
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    WarningOutlined,
    WifiOutlined,
} from '@ant-design/icons';
import { useSignatureDebugQuery } from '../hooks/useSignatureDebugQuery';
import type { SignatureDiagnosticStepDto } from '../types/signature-debug';

const { Text } = Typography;

interface SignatureStatusPanelProps {
    paymentId: string | null;
}

function StatusTag({ status }: { status: string }) {
    if (status === 'PASS') {
        return (
            <Tag icon={<CheckCircleOutlined />} color="success">
                PASS
            </Tag>
        );
    }
    if (status === 'FAIL') {
        return (
            <Tag icon={<CloseCircleOutlined />} color="error">
                FAIL
            </Tag>
        );
    }
    if (status === 'WARN') {
        return (
            <Tag icon={<WarningOutlined />} color="warning">
                WARN
            </Tag>
        );
    }
    return <Tag>{status}</Tag>;
}

export default function SignatureStatusPanel({ paymentId }: SignatureStatusPanelProps) {
    const { data, isLoading, isError, error } = useSignatureDebugQuery(paymentId);
    const isOffline = typeof navigator !== 'undefined' && !navigator.onLine;

    if (!paymentId) {
        return (
            <Card title="Signature Status">
                <Alert
                    type="info"
                    message="No payment linked"
                    description="This receipt has no associated payment. Signature verification is not available."
                />
            </Card>
        );
    }

    if (isOffline) {
        return (
            <Card title="Signature Status">
                <Alert
                    type="warning"
                    icon={<WifiOutlined />}
                    message="Offline"
                    description="You are offline. Signature verification requires a network connection. Connect to the internet to check RKSV checklist status."
                    showIcon
                />
            </Card>
        );
    }

    if (isLoading) {
        return (
            <Card title="Signature Status">
                <Spin tip="Verifying signature..." />
            </Card>
        );
    }

    if (isError) {
        return (
            <Card title="Signature Status">
                <Alert
                    type="error"
                    message="Verification failed"
                    description={(error as Error)?.message ?? 'Could not load signature diagnostic.'}
                    showIcon
                />
            </Card>
        );
    }

    const steps = data?.data ?? [];
    const hasFail = steps.some((s) => s.status === 'FAIL');
    const failSteps = steps.filter((s) => s.status === 'FAIL');

    return (
        <Card title="Signature Status">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {steps.map((step: SignatureDiagnosticStepDto) => (
                    <div
                        key={step.stepId}
                        style={{
                            display: 'flex',
                            alignItems: 'flex-start',
                            justifyContent: 'space-between',
                            gap: 12,
                            padding: '8px 0',
                            borderBottom:
                                step.stepId < steps.length
                                    ? '1px solid #f0f0f0'
                                    : undefined,
                        }}
                    >
                        <div style={{ flex: 1 }}>
                            <Text strong>{step.name}</Text>
                            {step.evidence && (
                                <div style={{ marginTop: 4, fontSize: 12, color: '#666' }}>
                                    {step.evidence}
                                </div>
                            )}
                        </div>
                        <StatusTag status={step.status} />
                    </div>
                ))}
            </div>

            {hasFail && failSteps.length > 0 && (
                <Collapse
                    style={{ marginTop: 16 }}
                    items={[
                        {
                            key: '1',
                            label: 'Technical details (failed steps)',
                            children: (
                                <div style={{ fontFamily: 'monospace', fontSize: 12 }}>
                                    {failSteps.map((s) => (
                                        <div key={s.stepId} style={{ marginBottom: 8 }}>
                                            <strong>
                                                Step {s.stepId}: {s.name}
                                            </strong>
                                            <br />
                                            {s.evidence ?? 'No evidence'}
                                        </div>
                                    ))}
                                </div>
                            ),
                        },
                    ]}
                />
            )}
        </Card>
    );
}
