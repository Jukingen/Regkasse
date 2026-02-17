'use client';

import React from 'react';
import { Descriptions, Tag, Typography } from 'antd';
import type { ReceiptDetailDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';
import dayjs from 'dayjs';

const { Text } = Typography;

interface ReceiptDetailCardProps {
    receipt: ReceiptDetailDto;
}

/**
 * Header card showing receipt metadata (number, dates, totals, signature info).
 */
export default function ReceiptDetailCard({ receipt }: ReceiptDetailCardProps) {
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
            <Descriptions.Item label="Issued At">
                {dayjs(receipt.issuedAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label="Created At">
                {dayjs(receipt.createdAt).format('DD.MM.YYYY HH:mm:ss')}
            </Descriptions.Item>
            <Descriptions.Item label="Cash Register">
                <Tag>{receipt.cashRegisterId}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Cashier">
                {receipt.cashierId || '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Payment ID">
                {receipt.paymentId ? (
                    <Text copyable style={{ fontSize: 12 }}>{receipt.paymentId}</Text>
                ) : '—'}
            </Descriptions.Item>
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
