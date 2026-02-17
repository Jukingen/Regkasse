'use client';

import React, { useState } from 'react';
import { Form, Input, InputNumber, Button, Card, Space, message, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import type { GenerateReceiptRequest, ReceiptItem } from '@/api/generated/model';

const { TextArea } = Input;
const { Paragraph } = Typography;

interface GenerateReceiptFormProps {
    onGenerate: (request: GenerateReceiptRequest) => Promise<string>;
    loading: boolean;
}

export default function GenerateReceiptForm({ onGenerate, loading }: GenerateReceiptFormProps) {
    const [form] = Form.useForm();
    const [generatedContent, setGeneratedContent] = useState<string | null>(null);

    const handleFinish = async (values: Record<string, unknown>) => {
        const items: ReceiptItem[] = values.items
            ? JSON.parse(values.items as string)
            : [];

        const request: GenerateReceiptRequest = {
            language: values.language as string,
            templateType: values.templateType as string,
            companyInfo: values.companyName
                ? {
                    name: values.companyName as string,
                    address: values.companyAddress as string,
                    phone: values.companyPhone as string,
                    email: values.companyEmail as string,
                    taxNumber: values.companyTaxNumber as string,
                }
                : undefined,
            customerInfo: values.customerName
                ? {
                    name: values.customerName as string,
                    address: values.customerAddress as string,
                    phone: values.customerPhone as string,
                }
                : undefined,
            items,
            taxAmount: values.taxAmount as number,
            totalAmount: values.totalAmount as number,
            paymentMethod: values.paymentMethod as string,
        };

        const content = await onGenerate(request);
        setGeneratedContent(content);
    };

    const copyToClipboard = () => {
        if (generatedContent) {
            navigator.clipboard.writeText(generatedContent);
            message.success('Copied to clipboard');
        }
    };

    return (
        <div>
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                <Card title="Template Selection" style={{ marginBottom: 16 }}>
                    <Form.Item
                        label="Language"
                        name="language"
                        rules={[
                            { required: true, message: 'Required' },
                            { min: 1, max: 10, message: '1-10 chars' },
                        ]}
                    >
                        <Input placeholder="en, de, tr" maxLength={10} />
                    </Form.Item>
                    <Form.Item
                        label="Template Type"
                        name="templateType"
                        rules={[
                            { required: true, message: 'Required' },
                            { min: 1, max: 50, message: '1-50 chars' },
                        ]}
                    >
                        <Input placeholder="sale, refund, invoice" maxLength={50} />
                    </Form.Item>
                </Card>

                <Card title="Company Info" style={{ marginBottom: 16 }}>
                    <Form.Item label="Name" name="companyName">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Address" name="companyAddress">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Phone" name="companyPhone">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Email" name="companyEmail">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Tax Number" name="companyTaxNumber">
                        <Input />
                    </Form.Item>
                </Card>

                <Card title="Customer Info" style={{ marginBottom: 16 }}>
                    <Form.Item label="Name" name="customerName">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Address" name="customerAddress">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Phone" name="customerPhone">
                        <Input />
                    </Form.Item>
                </Card>

                <Card title="Receipt Details" style={{ marginBottom: 16 }}>
                    <Form.Item
                        label="Items (JSON array)"
                        name="items"
                        tooltip='Format: [{"name":"Product","quantity":1,"unitPrice":10,"totalPrice":10}]'
                    >
                        <TextArea rows={4} placeholder='[{"name":"...","quantity":1,...}]' />
                    </Form.Item>
                    <Form.Item label="Tax Amount" name="taxAmount">
                        <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item label="Total Amount" name="totalAmount">
                        <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item label="Payment Method" name="paymentMethod">
                        <Input placeholder="Cash, Card, etc." />
                    </Form.Item>
                </Card>

                <Button type="primary" htmlType="submit" loading={loading} size="large">
                    Generate Receipt
                </Button>
            </Form>

            {generatedContent && (
                <Card
                    title="Generated Receipt"
                    style={{ marginTop: 24 }}
                    extra={
                        <Button icon={<CopyOutlined />} onClick={copyToClipboard}>
                            Copy
                        </Button>
                    }
                >
                    <Paragraph>
                        <pre style={{ background: '#f5f5f5', padding: 16, whiteSpace: 'pre-wrap' }}>
                            {generatedContent}
                        </pre>
                    </Paragraph>
                </Card>
            )}
        </div>
    );
}
