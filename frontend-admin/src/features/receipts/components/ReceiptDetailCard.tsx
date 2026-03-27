'use client';

import React from 'react';
import Link from 'next/link';
import { Alert, Descriptions, Tag, Typography } from 'antd';
import type { ReceiptDetailDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';
import {
    analyzeRegisterFkField,
    buildFinanzOnlineQueuePath,
    formatRegisterDisplayLabel,
    toLinkSafeRegisterRowId,
} from '@/shared/utils/registerIdentity';
import dayjs from 'dayjs';
import { OPERATOR_LINK_LABELS, OPERATOR_REGISTER_LINK_COPY } from '@/shared/operatorTruthCopy';
import { useI18n } from '@/i18n';

const { Text } = Typography;

interface ReceiptDetailCardProps {
    receipt: ReceiptDetailDto;
}

/**
 * Üst kart: fiş meta verisi (numara, tarihler, toplamlar, imza).
 */
export default function ReceiptDetailCard({ receipt }: ReceiptDetailCardProps) {
    const { t } = useI18n();
    const regFk = analyzeRegisterFkField(receipt.cashRegisterId);
    const c = (key: string) => t(`receipts.detail.card.${key}`);

    return (
        <Descriptions
            bordered
            column={{ xs: 1, sm: 2, md: 3 }}
            size="middle"
            title={t('receipts.detail.card.title', { number: receipt.receiptNumber })}
        >
            <Descriptions.Item label={c('labelReceiptNumber')}>
                <Text strong>{receipt.receiptNumber}</Text>
            </Descriptions.Item>
            <Descriptions.Item label={c('labelIssuedAt')}>
                {dayjs(receipt.issuedAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelPersistedUtc')}>
                {receipt.receiptPersistedAtUtc
                    ? dayjs(receipt.receiptPersistedAtUtc).format('DD.MM.YYYY HH:mm:ss')
                    : dayjs(receipt.createdAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelRegisterFk')}>
                {regFk.isRawPresentButNotLinkSafe ? (
                    <Alert
                        type="warning"
                        showIcon
                        style={{ marginBottom: 8 }}
                        message={c('registerUnsafeTitle')}
                        description={c('registerUnsafeDescription')}
                    />
                ) : null}
                <Text code copyable>{receipt.cashRegisterId || '—'}</Text>
                {regFk.linkSafeUuid ? (
                    <div style={{ marginTop: 8 }}>
                        <Link
                            href={buildFinanzOnlineQueuePath({
                                registerRowId: toLinkSafeRegisterRowId(receipt.cashRegisterId),
                            })}
                            target="_blank"
                            rel="noopener noreferrer"
                        >
                            {OPERATOR_LINK_LABELS.finanzQueueThisRegister}
                        </Link>
                    </div>
                ) : regFk.rawTrimmed ? (
                    <Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                        {OPERATOR_REGISTER_LINK_COPY.noMachineUuidHint}
                    </Text>
                ) : null}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelRegisterDisplay')}>
                {formatRegisterDisplayLabel(receipt.registerDisplayNumber) === '—' ? (
                    <Text type="secondary">—</Text>
                ) : (
                    <Tag>{formatRegisterDisplayLabel(receipt.registerDisplayNumber)}</Tag>
                )}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelCashier')}>
                {(receipt.cashierDisplayName && receipt.cashierDisplayName.trim()) ||
                    receipt.cashierId ||
                    '—'}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelPaymentId')}>
                {receipt.paymentId ? (
                    <div>
                        <Text copyable style={{ fontSize: 12 }}>{receipt.paymentId}</Text>
                        <div style={{ marginTop: 6 }}>
                            <Link href={`/payments?paymentId=${encodeURIComponent(receipt.paymentId)}`}>
                                {c('openPayment')}
                            </Link>
                        </div>
                    </div>
                ) : '—'}
            </Descriptions.Item>
            {receipt.hasOfflineOrigin ? (
                <Descriptions.Item label={c('labelOfflineOrigin')} span={3}>
                    <Tag color="purple">{c('tagReplay')}</Tag>
                    {receipt.clockDriftWarning ? (
                        <Tag color="red" style={{ marginLeft: 8 }}>
                            {c('tagClockDrift')}
                        </Tag>
                    ) : null}
                    {receipt.sequenceGapDetected ? (
                        <Tag color="orange" style={{ marginLeft: 8 }}>
                            {c('tagSequenceGap')}
                        </Tag>
                    ) : null}
                    {receipt.sequenceDuplicateDetected ? (
                        <Tag color="red" style={{ marginLeft: 8 }}>
                            {c('tagSequenceDuplicate')}
                        </Tag>
                    ) : null}
                    {receipt.offlineTransactionId ? (
                        <Text type="secondary" copyable style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                            {c('offlineIdPrefix')} {receipt.offlineTransactionId}
                        </Text>
                    ) : null}
                    {receipt.offlineCreatedAtUtc ? (
                        <Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                            {c('offlineCapturedUtc')}{' '}
                            {dayjs(receipt.offlineCreatedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                        </Text>
                    ) : null}
                    {receipt.fiscalizedAtUtc ? (
                        <Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                            {c('fiscalizedAfterReplayUtc')}{' '}
                            {dayjs(receipt.fiscalizedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                        </Text>
                    ) : null}
                </Descriptions.Item>
            ) : null}
            {receipt.fiscalTraceKind && (
                <Descriptions.Item label={c('labelFiscalTrace')}>
                    <Tag color="orange">{receipt.fiscalTraceKind}</Tag>
                    {receipt.originalPaymentId ? (
                        <div style={{ marginTop: 4 }}>
                            <Text type="secondary" copyable style={{ fontSize: 11, display: 'block' }}>
                                {c('originalPaymentPrefix')} {receipt.originalPaymentId}
                            </Text>
                            <Link href={`/payments?paymentId=${encodeURIComponent(receipt.originalPaymentId)}`}>
                                {c('openOriginalPayment')}
                            </Link>
                        </div>
                    ) : null}
                    {receipt.originalSaleReceiptId ? (
                        <div style={{ marginTop: 8 }}>
                            <Link href={`/receipts/${receipt.originalSaleReceiptId}`}>
                                {c('openOriginalSaleReceipt')}
                            </Link>
                        </div>
                    ) : null}
                </Descriptions.Item>
            )}
            <Descriptions.Item label={c('labelSubTotal')}>
                {formatEUR(receipt.subTotal)}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelTaxTotal')}>
                {formatEUR(receipt.taxTotal)}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelGrandTotal')}>
                <Text strong style={{ fontSize: 16 }}>{formatEUR(receipt.grandTotal)}</Text>
            </Descriptions.Item>
            <Descriptions.Item label={c('labelQrCode')} span={3}>
                {receipt.qrCodePayload ? (
                    <Text code style={{ fontSize: 11, wordBreak: 'break-all' }}>
                        {receipt.qrCodePayload}
                    </Text>
                ) : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={c('labelSignature')} span={3}>
                {receipt.signatureValue ? (
                    <Text code style={{ fontSize: 11, wordBreak: 'break-all' }}>
                        {receipt.signatureValue}
                    </Text>
                ) : '—'}
            </Descriptions.Item>
        </Descriptions>
    );
}
