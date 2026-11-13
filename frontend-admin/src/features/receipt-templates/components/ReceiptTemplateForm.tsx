'use client';

/**
 * Belegvorlage oluşturma/düzenleme formu; alanlar API şemasıyla uyumludur.
 */
import { Button, Checkbox, Form, Input, Space } from 'antd';
import React from 'react';

import type {
  CreateReceiptTemplateRequest,
  ReceiptTemplate,
  UpdateReceiptTemplateRequest,
} from '@/api/generated/model';
import { useI18n } from '@/i18n';

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
  const { t } = useI18n();
  const [form] = Form.useForm();

  const handleFinish = (values: CreateReceiptTemplateRequest) => {
    onSubmit(values);
  };

  const req = t('receiptTemplates.form.validationRequired');

  return (
    <Form form={form} layout="vertical" initialValues={initialValues} onFinish={handleFinish}>
      <Form.Item
        label={t('receiptTemplates.form.templateName')}
        name="templateName"
        rules={[
          { required: true, message: req },
          { min: 1, max: 100, message: t('receiptTemplates.form.validationNameLength') },
        ]}
      >
        <Input placeholder={t('receiptTemplates.form.templateNamePlaceholder')} maxLength={100} />
      </Form.Item>

      {mode === 'create' && (
        <Form.Item
          label={t('receiptTemplates.form.language')}
          name="language"
          rules={[
            { required: true, message: req },
            { min: 1, max: 10, message: t('receiptTemplates.form.validationLangLength') },
          ]}
        >
          <Input placeholder={t('receiptTemplates.form.languagePlaceholder')} maxLength={10} />
        </Form.Item>
      )}

      {mode === 'create' && (
        <Form.Item
          label={t('receiptTemplates.form.templateType')}
          name="templateType"
          rules={[
            { required: true, message: req },
            { min: 1, max: 50, message: t('receiptTemplates.form.validationTypeLength') },
          ]}
        >
          <Input placeholder={t('receiptTemplates.form.templateTypePlaceholder')} maxLength={50} />
        </Form.Item>
      )}

      <Form.Item label={t('receiptTemplates.form.headerTemplate')} name="headerTemplate">
        <TextArea
          rows={3}
          maxLength={2000}
          placeholder={t('receiptTemplates.form.placeholderHeader')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.companyTemplate')} name="companyTemplate">
        <TextArea
          rows={2}
          maxLength={1000}
          placeholder={t('receiptTemplates.form.placeholderCompany')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.customerTemplate')} name="customerTemplate">
        <TextArea
          rows={2}
          maxLength={1000}
          placeholder={t('receiptTemplates.form.placeholderCustomer')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.itemTemplate')} name="itemTemplate">
        <TextArea
          rows={2}
          maxLength={1000}
          placeholder={t('receiptTemplates.form.placeholderItem')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.taxTemplate')} name="taxTemplate">
        <TextArea
          rows={2}
          maxLength={500}
          placeholder={t('receiptTemplates.form.placeholderTax')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.totalTemplate')} name="totalTemplate">
        <TextArea
          rows={2}
          maxLength={500}
          placeholder={t('receiptTemplates.form.placeholderTotal')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.paymentTemplate')} name="paymentTemplate">
        <TextArea
          rows={2}
          maxLength={500}
          placeholder={t('receiptTemplates.form.placeholderPayment')}
        />
      </Form.Item>

      <Form.Item label={t('receiptTemplates.form.footerTemplate')} name="footerTemplate">
        <TextArea
          rows={3}
          maxLength={2000}
          placeholder={t('receiptTemplates.form.placeholderFooter')}
        />
      </Form.Item>

      <Form.Item name="isDefault" valuePropName="checked">
        <Checkbox>{t('receiptTemplates.form.checkboxDefault')}</Checkbox>
      </Form.Item>

      <Space>
        <Button type="primary" htmlType="submit" loading={loading}>
          {mode === 'create'
            ? t('receiptTemplates.form.submitCreate')
            : t('receiptTemplates.form.submitUpdate')}
        </Button>
      </Space>
    </Form>
  );
}
