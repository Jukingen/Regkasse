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
import { useI18n } from '@/i18n';

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
    const { t } = useI18n();
    const s = (key: string) => t(`receipts.detail.signature.${key}`);
    const { data, isLoading, isError, error } = useSignatureDebugQuery(paymentId);
    const isOffline = typeof navigator !== 'undefined' && !navigator.onLine;

    if (!paymentId) {
        return (
            <Card title={s('cardTitle')}>
                <Alert
                    type="info"
                    title={s('noPaymentTitle')}
                    description={s('noPaymentDescription')}
                />
            </Card>
        );
    }

    const showOfflineTimeline =
        offlineTrace?.hasOfflineOrigin &&
        (offlineTrace.offlineCreatedAtUtc || offlineTrace.fiscalizedAtUtc || offlineTrace.issuedAt);

    if (isOffline) {
        return (
            <Card title={s('cardTitle')}>
                <Alert
                    type="warning"
                    icon={<WifiOutlined />}
                    title={s('offlineTitle')}
                    description={s('offlineDescription')}
                    showIcon
                />
            </Card>
        );
    }

    if (isLoading) {
        return (
            <Card title={s('cardTitle')}>
                <Spin description={s('verifyingTip')} />
            </Card>
        );
    }

    if (isError) {
        return (
            <Card title={s('cardTitle')}>
                <Alert
                    type="error"
                    title={s('verificationFailed')}
                    description={(error as Error)?.message ?? s('loadDiagnosticFallback')}
                    showIcon
                />
            </Card>
        );
    }

    const payload = data?.data ?? { steps: [], compactJws: null };
    const steps = payload.steps;
    const compactJws = payload.compactJws;
    const hasFail = steps.some((st) => st.status === 'FAIL');
    const failSteps = steps.filter((st) => st.status === 'FAIL');

    return (
        <Card title={s('cardTitle')}>
            {showOfflineTimeline ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={s('timelineTitle')}
                    description={
                        <div style={{ fontSize: 12 }}>
                            {offlineTrace?.offlineCreatedAtUtc ? (
                                <div>
                                    <strong>{s('offlineCapturedStrong')}</strong>{' '}
                                    {dayjs(offlineTrace.offlineCreatedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                                </div>
                            ) : null}
                            {offlineTrace?.fiscalizedAtUtc ? (
                                <div style={{ marginTop: 4 }}>
                                    <strong>{s('fiscalizedAfterReplayStrong')}</strong>{' '}
                                    {dayjs(offlineTrace.fiscalizedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                                </div>
                            ) : null}
                            {offlineTrace?.issuedAt ? (
                                <div style={{ marginTop: 4 }}>
                                    <strong>{s('issuedAtFiscalStrong')}</strong>{' '}
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
                            label: s('collapseFailedSteps'),
                            children: (
                                <div style={{ fontFamily: 'monospace', fontSize: 12 }}>
                                    {failSteps.map((st) => (
                                        <div key={st.stepId} style={{ marginBottom: 8 }}>
                                            <strong>
                                                {t('receipts.detail.signature.stepLine', {
                                                    stepId: st.stepId,
                                                    name: st.name,
                                                })}
                                            </strong>
                                            <br />
                                            {st.evidence ?? s('noEvidence')}
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
                            label: s('compactJws'),
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
