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
} from '@/shared/utils/registerIdentity';
import dayjs from 'dayjs';
import { OPERATOR_LINK_LABELS, OPERATOR_REGISTER_LINK_COPY } from '@/shared/operatorTruthCopy';

const { Text } = Typography;

interface ReceiptDetailCardProps {
    receipt: ReceiptDetailDto;
}

/**
 * Header card showing receipt metadata (number, dates, totals, signature info).
 */
export default function ReceiptDetailCard({ receipt }: ReceiptDetailCardProps) {
    const regFk = analyzeRegisterFkField(receipt.cashRegisterId);
    return (
        <Descriptions
            bordered
            column={{ xs: 1, sm: 2, md: 3 }}
            size="middle"
            title={`Receipt ${receipt.receiptNumber}`}
        >
            <Descriptions.Item label="Receipt Number">
                <Text strong>{receipt.receiptNumber}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="Belegzeit (fiskal)">
                {dayjs(receipt.issuedAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label="Quittung gespeichert (UTC)">
                {receipt.receiptPersistedAtUtc
                    ? dayjs(receipt.receiptPersistedAtUtc).format('DD.MM.YYYY HH:mm:ss')
                    : dayjs(receipt.createdAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label="Register (FK, nur Maschine)">
                {regFk.isRawPresentButNotLinkSafe ? (
                    <Alert
                        type="warning"
                        showIcon
                        style={{ marginBottom: 8 }}
                        message="API-Registerwert ohne link-sichere UUID"
                        description="Wert wird unverändert angezeigt. Deep-Link zur Abgleichsseite ist deaktiviert, damit kein falscher Identifier in der URL landet."
                    />
                ) : null}
                <Text code copyable>{receipt.cashRegisterId || '—'}</Text>
                {regFk.linkSafeUuid ? (
                    <div style={{ marginTop: 8 }}>
                        <Link
                            href={buildFinanzOnlineQueuePath({ registerRowId: receipt.cashRegisterId })}
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
            <Descriptions.Item label="Kassen-ID / Nummer (Anzeige)">
                {formatRegisterDisplayLabel(receipt.registerDisplayNumber) === '—' ? (
                    <Text type="secondary">—</Text>
                ) : (
                    <Tag>{formatRegisterDisplayLabel(receipt.registerDisplayNumber)}</Tag>
                )}
            </Descriptions.Item>
            <Descriptions.Item label="Kassierer (Snapshot)">
                {(receipt.cashierDisplayName && receipt.cashierDisplayName.trim()) ||
                    receipt.cashierId ||
                    '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Payment ID">
                {receipt.paymentId ? (
                    <div>
                        <Text copyable style={{ fontSize: 12 }}>{receipt.paymentId}</Text>
                        <div style={{ marginTop: 6 }}>
                            <Link href={`/payments?paymentId=${encodeURIComponent(receipt.paymentId)}`}>
                                Payment öffnen
                            </Link>
                        </div>
                    </div>
                ) : '—'}
            </Descriptions.Item>
            {receipt.hasOfflineOrigin ? (
                <Descriptions.Item label="Offline-Ursprung" span={3}>
                    <Tag color="purple">Nachgestellt (Replay)</Tag>
                    {receipt.clockDriftWarning ? (
                        <Tag color="red" style={{ marginLeft: 8 }}>
                            Uhrzeit-Drift
                        </Tag>
                    ) : null}
                    {receipt.sequenceGapDetected ? (
                        <Tag color="orange" style={{ marginLeft: 8 }}>
                            Sequenz-Lücke
                        </Tag>
                    ) : null}
                    {receipt.sequenceDuplicateDetected ? (
                        <Tag color="red" style={{ marginLeft: 8 }}>
                            Sequenz-Duplikat
                        </Tag>
                    ) : null}
                    {receipt.offlineTransactionId ? (
                        <Text type="secondary" copyable style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                            Offline-ID: {receipt.offlineTransactionId}
                        </Text>
                    ) : null}
                    {receipt.offlineCreatedAtUtc ? (
                        <Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                            Offline erfasst (Gerät, UTC):{' '}
                            {dayjs(receipt.offlineCreatedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                        </Text>
                    ) : null}
                    {receipt.fiscalizedAtUtc ? (
                        <Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                            Fiskalisiert nach Replay (Server, UTC):{' '}
                            {dayjs(receipt.fiscalizedAtUtc).format('DD.MM.YYYY HH:mm:ss')}
                        </Text>
                    ) : null}
                </Descriptions.Item>
            ) : null}
            {receipt.fiscalTraceKind && (
                <Descriptions.Item label="Fiscal trace (reversal)">
                    <Tag color="orange">{receipt.fiscalTraceKind}</Tag>
                    {receipt.originalPaymentId ? (
                        <div style={{ marginTop: 4 }}>
                            <Text type="secondary" copyable style={{ fontSize: 11, display: 'block' }}>
                                Original payment: {receipt.originalPaymentId}
                            </Text>
                            <Link href={`/payments?paymentId=${encodeURIComponent(receipt.originalPaymentId)}`}>
                                Original payment öffnen
                            </Link>
                        </div>
                    ) : null}
                    {receipt.originalSaleReceiptId ? (
                        <div style={{ marginTop: 8 }}>
                            <Link href={`/receipts/${receipt.originalSaleReceiptId}`}>
                                Open original sale receipt
                            </Link>
                        </div>
                    ) : null}
                </Descriptions.Item>
            )}
            <Descriptions.Item label="Sub Total">
                {formatEUR(receipt.subTotal)}
            </Descriptions.Item>
            <Descriptions.Item label="Tax Total">
                {formatEUR(receipt.taxTotal)}
            </Descriptions.Item>
            <Descriptions.Item label="Grand Total">
                <Text strong style={{ fontSize: 16 }}>{formatEUR(receipt.grandTotal)}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="QR Code" span={3}>
                {receipt.qrCodePayload ? (
                    <Text code style={{ fontSize: 11, wordBreak: 'break-all' }}>
                        {receipt.qrCodePayload}
                    </Text>
                ) : '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Signature" span={3}>
                {receipt.signatureValue ? (
                    <Text code style={{ fontSize: 11, wordBreak: 'break-all' }}>
                        {receipt.signatureValue}
                    </Text>
                ) : '—'}
            </Descriptions.Item>
        </Descriptions>
    );
}
