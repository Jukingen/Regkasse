'use client';

import React from 'react';
import { Form, Input, Select, DatePicker, Button, Space, Col, Row } from 'antd';
import { SearchOutlined, ClearOutlined } from '@ant-design/icons';
import type { ReceiptListParams } from '@/features/receipts/types/receipts';
import dayjs from 'dayjs';

const { RangePicker } = DatePicker;

interface ReceiptsFilterBarProps {
    initialValues: Partial<ReceiptListParams>;
    onFilterChange: (values: Partial<ReceiptListParams>) => void;
    onReset: () => void;
    loading: boolean;
}

interface FilterFormValues {
    receiptNumber?: string;
    cashRegisterId?: string;
    cashierId?: string;
    dateRange?: [dayjs.Dayjs, dayjs.Dayjs] | null;
}

/**
 * Filter bar for the receipts list.
 * Purely presentational — does not touch URL or queries directly.
 */
export default function ReceiptsFilterBar({
    initialValues,
    onFilterChange,
    onReset,
    loading,
}: ReceiptsFilterBarProps) {
    const [form] = Form.useForm<FilterFormValues>();

    const handleFinish = (values: FilterFormValues) => {
        const params: Partial<ReceiptListParams> = {};
        if (values.receiptNumber) params.receiptNumber = values.receiptNumber;
        if (values.cashRegisterId) params.cashRegisterId = values.cashRegisterId;
        if (values.cashierId) params.cashierId = values.cashierId;
        if (values.dateRange?.[0]) params.issuedFrom = values.dateRange[0].toISOString();
        if (values.dateRange?.[1]) params.issuedTo = values.dateRange[1].toISOString();
        onFilterChange(params);
    };

    const handleReset = () => {
        form.resetFields();
        onReset();
    };

    // Map ReceiptListParams back to form field values for initial state
    const formInitial: FilterFormValues = {
        receiptNumber: initialValues.receiptNumber,
        cashRegisterId: initialValues.cashRegisterId,
        cashierId: initialValues.cashierId,
        dateRange:
            initialValues.issuedFrom && initialValues.issuedTo
                ? [dayjs(initialValues.issuedFrom), dayjs(initialValues.issuedTo)]
                : undefined,
    };

    return (
        <Form
            form={form}
            layout="inline"
            initialValues={formInitial}
            onFinish={handleFinish}
            style={{ marginBottom: 16, flexWrap: 'wrap', gap: 8 }}
        >
            <Form.Item name="receiptNumber">
                <Input
                    placeholder="Receipt number"
                    prefix={<SearchOutlined />}
                    allowClear
                    style={{ width: 180 }}
                />
            </Form.Item>

            <Form.Item name="cashRegisterId">
                <Input
                    placeholder="Cash register ID"
                    allowClear
                    style={{ width: 160 }}
                />
            </Form.Item>

            <Form.Item name="cashierId">
                <Input
                    placeholder="Cashier ID"
                    allowClear
                    style={{ width: 140 }}
                />
            </Form.Item>

            <Form.Item name="dateRange">
                <RangePicker
                    format="DD.MM.YYYY"
                    placeholder={['From', 'To']}
                />
            </Form.Item>

            <Form.Item>
                <Space>
                    <Button
                        type="primary"
                        htmlType="submit"
                        icon={<SearchOutlined />}
                        loading={loading}
                    >
                        Search
                    </Button>
                    <Button
                        onClick={handleReset}
                        icon={<ClearOutlined />}
                    >
                        Reset
                    </Button>
                </Space>
            </Form.Item>
        </Form>
    );
}
