'use client';

import { CopyOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Form, Input, InputNumber, Select, Typography } from 'antd';
import React, { useEffect, useRef, useState } from 'react';

import type { GenerateReceiptRequest, ReceiptItem } from '@/api/generated/model';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

import { useReceiptTemplates } from '../hooks/useReceiptTemplates';

const { TextArea } = Input;
const { Paragraph } = Typography;

interface GenerateReceiptFormProps {
  onGenerate: (request: GenerateReceiptRequest) => Promise<string>;
  loading: boolean;
  /** When set (e.g. from URL query), pre-select this template once templates are loaded. */
  initialTemplateId?: string;
}

export default function GenerateReceiptForm({
  onGenerate,
  loading,
  initialTemplateId,
}: GenerateReceiptFormProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const [form] = Form.useForm();
  const [generatedContent, setGeneratedContent] = useState<string | null>(null);
  const appliedInitialRef = useRef(false);

  const { useList } = useReceiptTemplates();
  const { data: templates = [], isLoading: templatesLoading } = useList();

  const itemsJsonError = t('receiptTemplates.generate.itemsJsonError');
  const selectTemplateForLangType = t('receiptTemplates.generate.selectTemplateForLangType');

  // Pre-select template from URL when list is loaded and template exists
  useEffect(() => {
    if (
      templatesLoading ||
      !initialTemplateId ||
      appliedInitialRef.current ||
      templates.length === 0
    ) {
      return;
    }
    const template = templates.find((tpl) => tpl.id === initialTemplateId);
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
    const template = templates.find((tpl) => tpl.id === templateId);
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
      form.setFields([{ name: 'templateId', errors: [selectTemplateForLangType] }]);
      return;
    }

    let items: ReceiptItem[] = [];
    const rawItems = values.items;
    if (rawItems && typeof rawItems === 'string' && rawItems.trim() !== '') {
      try {
        const parsed = JSON.parse(rawItems as string);
        if (!Array.isArray(parsed)) {
          form.setFields([{ name: 'items', errors: [itemsJsonError] }]);
          return;
        }
        items = parsed as ReceiptItem[];
      } catch {
        form.setFields([{ name: 'items', errors: [itemsJsonError] }]);
        return;
      }
    }

    const request: GenerateReceiptRequest = {
      language,
      templateType,
      companyInfo:
        values.companyName || values.companyAddress
          ? {
              name: values.companyName as string,
              address: values.companyAddress as string,
              phone: values.companyPhone as string,
              email: values.companyEmail as string,
              taxNumber: values.companyTaxNumber as string,
            }
          : undefined,
      customerInfo:
        values.customerName || values.customerAddress
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
      message.success(t('receiptTemplates.generate.copySuccess'));
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
        <Card title={t('receiptTemplates.generate.templateSelection')} style={{ marginBottom: 16 }}>
          <Form.Item
            label={t('receiptTemplates.generate.labelTemplate')}
            name="templateId"
            rules={[{ required: true, message: t('receiptTemplates.generate.selectTemplateRule') }]}
          >
            <Select
              placeholder={t('receiptTemplates.generate.placeholderTemplate')}
              loading={templatesLoading}
              notFoundContent={
                !templatesLoading && (!templates || templates.length === 0)
                  ? t('receiptTemplates.generate.notFoundContent')
                  : null
              }
              allowClear
              showSearch
              optionFilterProp="label"
              onChange={handleTemplateChange}
              options={templates
                .filter((tpl): tpl is typeof tpl & { id: string } => !!tpl.id)
                .map((tpl) => ({
                  value: tpl.id,
                  label: `${tpl.templateName} (${tpl.language} / ${tpl.templateType})`,
                }))}
            />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.form.language')} name="language">
            <Input readOnly placeholder={t('receiptTemplates.generate.languageReadOnly')} />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.form.templateType')} name="templateType">
            <Input readOnly placeholder={t('receiptTemplates.generate.typeReadOnly')} />
          </Form.Item>
        </Card>

        <Card title={t('receiptTemplates.generate.companyCard')} style={{ marginBottom: 16 }}>
          <Form.Item label={t('receiptTemplates.generate.labelCompanyName')} name="companyName">
            <Input />
          </Form.Item>
          <Form.Item
            label={t('receiptTemplates.generate.labelCompanyAddress')}
            name="companyAddress"
          >
            <Input />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelCompanyPhone')} name="companyPhone">
            <Input />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelCompanyEmail')} name="companyEmail">
            <Input />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelCompanyTax')} name="companyTaxNumber">
            <Input />
          </Form.Item>
        </Card>

        <Card title={t('receiptTemplates.generate.customerCard')} style={{ marginBottom: 16 }}>
          <Form.Item label={t('receiptTemplates.generate.labelCustomerName')} name="customerName">
            <Input />
          </Form.Item>
          <Form.Item
            label={t('receiptTemplates.generate.labelCustomerAddress')}
            name="customerAddress"
          >
            <Input />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelCustomerPhone')} name="customerPhone">
            <Input />
          </Form.Item>
        </Card>

        <Card
          title={t('receiptTemplates.generate.receiptDetailsCard')}
          style={{ marginBottom: 16 }}
        >
          <Form.Item
            label={t('receiptTemplates.generate.labelItemsJson')}
            name="items"
            tooltip={t('receiptTemplates.generate.tooltipItemsJson')}
          >
            <TextArea rows={4} placeholder={t('receiptTemplates.generate.placeholderItemsJson')} />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelTaxAmount')} name="taxAmount">
            <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelTotalAmount')} name="totalAmount">
            <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label={t('receiptTemplates.generate.labelPaymentMethod')} name="paymentMethod">
            <Input placeholder={t('receiptTemplates.generate.placeholderPaymentMethod')} />
          </Form.Item>
        </Card>

        <Button type="primary" htmlType="submit" loading={loading} size="large">
          {t('receiptTemplates.generate.submitPreview')}
        </Button>
      </Form>

      {generatedContent && (
        <Card
          title={t('receiptTemplates.generate.resultCardTitle')}
          style={{ marginTop: 24 }}
          extra={
            <Button icon={<CopyOutlined />} onClick={copyToClipboard}>
              {t('receiptTemplates.generate.copy')}
            </Button>
          }
        >
          <Alert
            type="warning"
            showIcon
            title={t('receiptTemplates.generate.resultWarningTitle')}
            description={t('receiptTemplates.generate.resultWarningDescription')}
            style={{ marginBottom: 16 }}
          />
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
