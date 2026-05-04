'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, Button, Space, Spin, Alert, Typography } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useReceiptDetailQuery } from '@/features/receipts/hooks/useReceiptDetailQuery';
import ReceiptDetailCard from '@/features/receipts/components/ReceiptDetailCard';
import ReceiptItemsTable from '@/features/receipts/components/ReceiptItemsTable';
import ReceiptTaxSummary from '@/features/receipts/components/ReceiptTaxSummary';
import SignatureStatusPanel from '@/features/receipts/components/SignatureStatusPanel';
import RksvSpecialReceiptFinanzOnlineSubmissionCard from '@/features/receipts/components/RksvSpecialReceiptFinanzOnlineSubmissionCard';
import { useI18n } from '@/i18n';

const { Title } = Typography;

/** True when the error is a 404 Not Found (receipt does not exist). */
function isNotFoundError(error: unknown): boolean {
    const status = (error as { response?: { status?: number }; normalized?: { status?: number } })?.response?.status
        ?? (error as { normalized?: { status?: number } })?.normalized?.status;
    return status === 404;
}

/** User-facing message for receipt detail error. */
function getReceiptDetailErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof Error) return error.message;
    const norm = (error as { normalized?: { message?: string } })?.normalized;
    if (norm?.message) return norm.message;
    return fallback;
}

export default function ReceiptDetailPage() {
    const { receiptId } = useParams<{ receiptId: string }>();
    const router = useRouter();
    const { t } = useI18n();
    const { data: receipt, isLoading, isError, error } = useReceiptDetailQuery(receiptId);

    const handleBack = () => {
        if (window.history.length > 1) {
            router.back();
        } else {
            router.push('/receipts');
        }
    };

    if (isLoading) {
        return <Spin style={{ display: 'block', margin: '80px auto' }} tip={t('receipts.detail.loadingTip')} />;
    }

    if (isError && isNotFoundError(error)) {
        return (
            <Alert
                type="warning"
                message={t('receipts.detail.notFoundTitle')}
                description={t('receipts.detail.notFoundDescription')}
                showIcon
                action={
                    <Button onClick={handleBack}>{t('receipts.detail.backToList')}</Button>
                }
            />
        );
    }

    if (isError) {
        return (
            <Alert
                type="error"
                message={t('receipts.detail.loadFailedTitle')}
                description={getReceiptDetailErrorMessage(error, t('receipts.detail.unexpectedError'))}
                showIcon
                action={
                    <Button onClick={handleBack}>{t('receipts.detail.backToList')}</Button>
                }
            />
        );
    }

    if (!receipt) {
        return (
            <Alert
                type="warning"
                message={t('receipts.detail.notFoundTitle')}
                description={t('receipts.detail.fallbackNoData')}
                showIcon
                action={
                    <Button onClick={handleBack}>{t('receipts.detail.backToList')}</Button>
                }
            />
        );
    }

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Button
                icon={<ArrowLeftOutlined />}
                onClick={handleBack}
                type="text"
            >
                {t('receipts.detail.backToList')}
            </Button>

            <ReceiptDetailCard receipt={receipt} />

            <RksvSpecialReceiptFinanzOnlineSubmissionCard
                receiptId={receipt.receiptId}
                paymentId={receipt.paymentId}
                rksvSpecialReceiptKind={receipt.rksvSpecialReceiptKind}
                submission={receipt.rksvFinanzOnlineSubmission ?? null}
            />

            <SignatureStatusPanel
                paymentId={receipt.paymentId}
                offlineTrace={
                    receipt.hasOfflineOrigin
                        ? {
                              hasOfflineOrigin: true,
                              offlineTransactionId: receipt.offlineTransactionId,
                              offlineCreatedAtUtc: receipt.offlineCreatedAtUtc ?? undefined,
                              fiscalizedAtUtc: receipt.fiscalizedAtUtc ?? undefined,
                              issuedAt: receipt.issuedAt,
                          }
                        : undefined
                }
            />

            <Card>
                <Title level={5} style={{ marginBottom: 12 }}>{t('receipts.detail.lineItemsTitle')}</Title>
                <ReceiptItemsTable items={receipt.items} />
            </Card>

            <Card>
                <Title level={5} style={{ marginBottom: 12 }}>{t('receipts.detail.taxBreakdownTitle')}</Title>
                <ReceiptTaxSummary
                    taxLines={receipt.taxLines}
                    subTotal={receipt.subTotal}
                    taxTotal={receipt.taxTotal}
                    grandTotal={receipt.grandTotal}
                />
            </Card>
        </Space>
    );
}
