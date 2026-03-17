'use client';

import React, { useState, useEffect, useRef } from 'react';
import { Form, Input, InputNumber, Button, Card, message, Select, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import type { GenerateReceiptRequest, ReceiptItem } from '@/api/generated/model';
import { useReceiptTemplates } from '../hooks/useReceiptTemplates';

const { TextArea } = Input;
const { Paragraph } = Typography;

const ITEMS_JSON_ERROR =
    'Invalid JSON. Use an array of objects, e.g. [{"name":"Product","quantity":1,"unitPrice":10,"totalPrice":10}]';

interface GenerateReceiptFormProps {
    onGenerate: (request: GenerateReceiptRequest) => Promise<string>;
    loading: boolean;
    /** When set (e.g. from URL query), pre-select this template once templates are loaded. */
    initialTemplateId?: string;
}

export default function GenerateReceiptForm({ onGenerate, loading, initialTemplateId }: GenerateReceiptFormProps) {
    const [form] = Form.useForm();
    const [generatedContent, setGeneratedContent] = useState<string | null>(null);
    const appliedInitialRef = useRef(false);

    const { useList } = useReceiptTemplates();
    const { data: templates = [], isLoading: templatesLoading } = useList();

    // Pre-select template from URL when list is loaded and template exists
    useEffect(() => {
        if (templatesLoading || !initialTemplateId || appliedInitialRef.current || templates.length === 0) {
            return;
        }
        const template = templates.find((t) => t.id === initialTemplateId);
        if (template) {
            form.setFieldsValue({
                templateId: template.id,
                language: template.language,
                templateType: template.templateType,
            });
            appliedInitialRef.current = true;
        }
    }, [templates, templatesLoading, initialTemplateId, form]);

    const handleTemplateChange = (templateId: string | undefined) => {
        if (!templateId) {
            form.setFieldsValue({ language: undefined, templateType: undefined });
            return;
        }
        const template = templates.find((t) => t.id === templateId);
        if (template) {
            form.setFieldsValue({
                language: template.language,
                templateType: template.templateType,
            });
        }
    };

    const handleFinish = async (values: Record<string, unknown>) => {
        const language = values.language as string | undefined;
        const templateType = values.templateType as string | undefined;
        if (!language || !templateType) {
            form.setFields([{ name: 'templateId', errors: ['Select a template to set language and type'] }]);
            return;
        }

        let items: ReceiptItem[] = [];
        const rawItems = values.items;
        if (rawItems && typeof rawItems === 'string' && rawItems.trim() !== '') {
            try {
                const parsed = JSON.parse(rawItems as string);
                if (!Array.isArray(parsed)) {
                    form.setFields([{ name: 'items', errors: [ITEMS_JSON_ERROR] }]);
                    return;
                }
                items = parsed as ReceiptItem[];
            } catch {
                form.setFields([{ name: 'items', errors: [ITEMS_JSON_ERROR] }]);
                return;
            }
        }

        const request: GenerateReceiptRequest = {
            language,
            templateType,
            companyInfo: (values.companyName || values.companyAddress)
                ? {
                    name: values.companyName as string,
                    address: values.companyAddress as string,
                    phone: values.companyPhone as string,
                    email: values.companyEmail as string,
                    taxNumber: values.companyTaxNumber as string,
                }
                : undefined,
            customerInfo: (values.customerName || values.customerAddress)
                ? {
                    name: values.customerName as string,
                    address: values.customerAddress as string,
                    phone: values.customerPhone as string,
                }
                : undefined,
            items,
            taxAmount: Number(values.taxAmount) || 0,
            totalAmount: Number(values.totalAmount) || 0,
            paymentMethod: values.paymentMethod as string,
        };

        try {
            const content = await onGenerate(request);
            setGeneratedContent(content ?? '');
        } catch {
            // Error already shown by mutation onError in page; keep previous result if any
        }
    };

    const copyToClipboard = () => {
        if (generatedContent) {
            navigator.clipboard.writeText(generatedContent);
            message.success('Copied to clipboard');
        }
    };

    return (
        <div>
            <Form
                form={form}
                layout="vertical"
                onFinish={handleFinish}
                onValuesChange={() => setGeneratedContent(null)}
            >
                <Card title="Template Selection" style={{ marginBottom: 16 }}>
                    <Form.Item
                        label="Template"
                        name="templateId"
                        rules={[{ required: true, message: 'Select a template' }]}
                    >
                        <Select
                            placeholder="Select a template"
                            loading={templatesLoading}
                            notFoundContent={
                                !templatesLoading && (!templates || templates.length === 0)
                                    ? 'No templates available'
                                    : null
                            }
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            onChange={handleTemplateChange}
                            options={templates
                                .filter((t): t is typeof t & { id: string } => !!t.id)
                                .map((t) => ({
                                    value: t.id,
                                    label: `${t.templateName} (${t.language} / ${t.templateType})`,
                                }))}
                        />
                    </Form.Item>
                    <Form.Item label="Language" name="language">
                        <Input readOnly placeholder="Set by template selection" />
                    </Form.Item>
                    <Form.Item label="Template Type" name="templateType">
                        <Input readOnly placeholder="Set by template selection" />
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
                    Vorschau erzeugen
                </Button>
            </Form>

            {generatedContent && (
                <Card
                    title="Vorschau (nicht fiskal)"
                    style={{ marginTop: 24 }}
                    extra={
                        <Button icon={<CopyOutlined />} onClick={copyToClipboard}>
                            Copy
                        </Button>
                    }
                >
                    <Paragraph type="secondary" style={{ marginBottom: 8 }}>
                        Dieser Inhalt ist keine fiskale Quittung. Nur zur Vorschau der Vorlage.
                    </Paragraph>
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
