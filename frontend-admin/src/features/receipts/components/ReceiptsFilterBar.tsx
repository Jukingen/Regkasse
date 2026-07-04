'use client';

import React from 'react';
import { Form, Input, DatePicker, Button, Space, Col, Row } from 'antd';
import { SearchOutlined, ClearOutlined } from '@ant-design/icons';
import type { ReceiptListParams } from '@/features/receipts/types/receipts';
import dayjs from 'dayjs';
import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { useI18n } from '@/i18n';

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
    const { t } = useI18n();
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
                    placeholder={t('receipts.filters.placeholderReceiptNumber')}
                    prefix={<SearchOutlined />}
                    allowClear
                    style={{ width: 180 }}
                />
            </Form.Item>

            <Form.Item name="cashRegisterId">
                <CashRegisterSelector
                    showFormItem={false}
                    required={false}
                    allowClear
                    placeholder={t('receipts.filters.placeholderCashRegister')}
                    style={{ width: 220 }}
                />
            </Form.Item>

            <Form.Item name="cashierId">
                <Input
                    placeholder={t('receipts.filters.placeholderCashier')}
                    allowClear
                    style={{ width: 140 }}
                />
            </Form.Item>

            <Form.Item name="dateRange">
                <RangePicker
                    format="DD.MM.YYYY"
                    placeholder={[t('receipts.filters.dateFrom'), t('receipts.filters.dateTo')]}
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
                        {t('receipts.filters.search')}
                    </Button>
                    <Button
                        onClick={handleReset}
                        icon={<ClearOutlined />}
                    >
                        {t('receipts.filters.reset')}
                    </Button>
                </Space>
            </Form.Item>
        </Form>
    );
}
