'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, Button, Space, Spin, Alert, Divider, Typography } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useReceiptDetailQuery } from '@/features/receipts/hooks/useReceiptDetailQuery';
import ReceiptDetailCard from '@/features/receipts/components/ReceiptDetailCard';
import ReceiptItemsTable from '@/features/receipts/components/ReceiptItemsTable';
import ReceiptTaxSummary from '@/features/receipts/components/ReceiptTaxSummary';
import SignatureStatusPanel from '@/features/receipts/components/SignatureStatusPanel';

const { Title } = Typography;

export default function ReceiptDetailPage() {
    const { receiptId } = useParams<{ receiptId: string }>();
    const router = useRouter();
    const { data: receipt, isLoading, isError, error } = useReceiptDetailQuery(receiptId);

    const handleBack = () => {
        // Use browser back if history exists (preserves list URL state),
        // otherwise navigate to the list page.
        if (window.history.length > 1) {
            router.back();
        } else {
            router.push('/receipts');
        }
    };

    if (isLoading) {
        return <Spin style={{ display: 'block', margin: '80px auto' }} />;
    }

    if (isError) {
        return (
            <Alert
                type="error"
                message="Failed to load receipt"
                description={(error as Error)?.message || 'An unexpected error occurred.'}
                showIcon
                action={
                    <Button onClick={handleBack}>Back to Receipts</Button>
                }
            />
        );
    }

    if (!receipt) {
        return (
            <Alert
                type="warning"
                message="Receipt not found"
                showIcon
                action={
                    <Button onClick={handleBack}>Back to Receipts</Button>
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
                Back to Receipts
            </Button>

            <ReceiptDetailCard receipt={receipt} />

            <SignatureStatusPanel paymentId={receipt.paymentId} />

            <Card>
                <Title level={5} style={{ marginBottom: 12 }}>Line Items</Title>
                <ReceiptItemsTable items={receipt.items} />
            </Card>

            <Card>
                <Title level={5} style={{ marginBottom: 12 }}>Tax Breakdown</Title>
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
