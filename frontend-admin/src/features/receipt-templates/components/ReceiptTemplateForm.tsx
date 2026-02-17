'use client';

import React from 'react';
import { Form, Input, Checkbox, Button, Space } from 'antd';
import type {
    CreateReceiptTemplateRequest,
    UpdateReceiptTemplateRequest,
    ReceiptTemplate,
} from '@/api/generated/model';

const { TextArea } = Input;

interface ReceiptTemplateFormProps {
    initialValues?: ReceiptTemplate;
    onSubmit: (values: CreateReceiptTemplateRequest | UpdateReceiptTemplateRequest) => void;
    loading: boolean;
    mode: 'create' | 'edit';
}

export default function ReceiptTemplateForm({
    initialValues,
    onSubmit,
    loading,
    mode,
}: ReceiptTemplateFormProps) {
    const [form] = Form.useForm();

    const handleFinish = (values: CreateReceiptTemplateRequest) => {
        onSubmit(values);
    };

    return (
        <Form
            form={form}
            layout="vertical"
            initialValues={initialValues}
            onFinish={handleFinish}
        >
            <Form.Item
                label="Template Name"
                name="templateName"
                rules={[
                    { required: true, message: 'Required' },
                    { min: 1, max: 100, message: 'Must be 1-100 chars' },
                ]}
            >
                <Input placeholder="e.g. Standard Receipt Template" maxLength={100} />
            </Form.Item>

            {mode === 'create' && (
                <Form.Item
                    label="Language"
                    name="language"
                    rules={[
                        { required: true, message: 'Required' },
                        { min: 1, max: 10, message: 'Must be 1-10 chars' },
                    ]}
                >
                    <Input placeholder="e.g. en, de, tr" maxLength={10} />
                </Form.Item>
            )}

            {mode === 'create' && (
                <Form.Item
                    label="Template Type"
                    name="templateType"
                    rules={[
                        { required: true, message: 'Required' },
                        { min: 1, max: 50, message: 'Must be 1-50 chars' },
                    ]}
                >
                    <Input placeholder="e.g. sale, refund, invoice" maxLength={50} />
                </Form.Item>
            )}

            <Form.Item label="Header Template" name="headerTemplate">
                <TextArea rows={3} maxLength={2000} placeholder="Header content..." />
            </Form.Item>

            <Form.Item label="Company Template" name="companyTemplate">
                <TextArea rows={2} maxLength={1000} placeholder="Company info..." />
            </Form.Item>

            <Form.Item label="Customer Template" name="customerTemplate">
                <TextArea rows={2} maxLength={1000} placeholder="Customer info..." />
            </Form.Item>

            <Form.Item label="Item Template" name="itemTemplate">
                <TextArea rows={2} maxLength={1000} placeholder="Item line..." />
            </Form.Item>

            <Form.Item label="Tax Template" name="taxTemplate">
                <TextArea rows={2} maxLength={500} placeholder="Tax line..." />
            </Form.Item>

            <Form.Item label="Total Template" name="totalTemplate">
                <TextArea rows={2} maxLength={500} placeholder="Total line..." />
            </Form.Item>

            <Form.Item label="Payment Template" name="paymentTemplate">
                <TextArea rows={2} maxLength={500} placeholder="Payment info..." />
            </Form.Item>

            <Form.Item label="Footer Template" name="footerTemplate">
                <TextArea rows={3} maxLength={2000} placeholder="Footer content..." />
            </Form.Item>

            <Form.Item name="isDefault" valuePropName="checked">
                <Checkbox>Set as default template</Checkbox>
            </Form.Item>

            <Space>
                <Button type="primary" htmlType="submit" loading={loading}>
                    {mode === 'create' ? 'Create Template' : 'Update Template'}
                </Button>
            </Space>
        </Form>
    );
}
