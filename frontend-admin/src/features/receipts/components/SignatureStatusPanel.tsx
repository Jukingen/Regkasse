'use client';

import React from 'react';
import { Card, Alert, Spin, Tag, Typography, Collapse } from 'antd';
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    WarningOutlined,
    WifiOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useSignatureDebugQuery } from '../hooks/useSignatureDebugQuery';
import type { SignatureDiagnosticStepDto } from '../types/signature-debug';

const { Text } = Typography;

export interface ReceiptOfflineTraceProps {
    hasOfflineOrigin: boolean;
    offlineTransactionId?: string | null;
    offlineCreatedAtUtc?: string | null;
    fiscalizedAtUtc?: string | null;
    issuedAt?: string | null;
}

interface SignatureStatusPanelProps {
    paymentId: string | null;
    /** RKSV trace: offline queue → replay → fiscal receipt timeline. */
    offlineTrace?: ReceiptOfflineTraceProps | null;
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

export default function SignatureStatusPanel({ paymentId, offlineTrace }: SignatureStatusPanelProps) {
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

    const showOfflineTimeline =
        offlineTrace?.hasOfflineOrigin &&
        (offlineTrace.offlineCreatedAtUtc || offlineTrace.fiscalizedAtUtc || offlineTrace.issuedAt);

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

    const payload = data?.data ?? { steps: [], compactJws: null };
    const steps = payload.steps;
    const compactJws = payload.compactJws;
    const hasFail = steps.some((s) => s.status === 'FAIL');
    const failSteps = steps.filter((s) => s.status === 'FAIL');

    return (
        <Card title="Signature Status">
            {showOfflineTimeline ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message="Offline → Replay → fiskaler Beleg"
                    description={
                        <div style={{ fontSize: 12 }}>
                            {offlineTrace?.offlineCreatedAtUtc ? (
                                <div>
                                    <strong>Offline erfasst (UTC):</strong>{' '}
                                    {dayjs(offlineTrace.offlineCreatedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                                </div>
                            ) : null}
                            {offlineTrace?.fiscalizedAtUtc ? (
                                <div style={{ marginTop: 4 }}>
                                    <strong>Nach Replay fiskalisiert (UTC):</strong>{' '}
                                    {dayjs(offlineTrace.fiscalizedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                                </div>
                            ) : null}
                            {offlineTrace?.issuedAt ? (
                                <div style={{ marginTop: 4 }}>
                                    <strong>Belegzeit (fiskal):</strong>{' '}
                                    {dayjs(offlineTrace.issuedAt).format('DD.MM.YYYY HH:mm:ss')}
                                </div>
                            ) : null}
                        </div>
                    }
                />
            ) : null}
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

            {compactJws ? (
                <Collapse
                    style={{ marginTop: 12 }}
                    items={[
                        {
                            key: 'jws',
                            label: 'Compact JWS (debug)',
                            children: (
                                <Text copyable style={{ fontFamily: 'monospace', fontSize: 11, wordBreak: 'break-all' }}>
                                    {compactJws}
                                </Text>
                            ),
                        },
                    ]}
                />
            ) : null}
        </Card>
    );
}
