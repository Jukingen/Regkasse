'use client';

import React, { useMemo } from 'react';
import { Form, Input, InputNumber, Modal, Select, Switch } from 'antd';

import type {
  CreatePaymentMethodDefinitionRequest,
  PaymentMethodDefinitionAdmin,
} from '@/api/admin/payment-method-definitions';
import { useI18n } from '@/i18n';

type PaymentMethodDefinitionModalProps = {
  open: boolean;
  editing: PaymentMethodDefinitionAdmin | null;
  cashRegisterId: string;
  confirmLoading: boolean;
  onCancel: () => void;
  onSubmit: (values: CreatePaymentMethodDefinitionRequest) => Promise<void>;
  form: ReturnType<typeof Form.useForm<CreatePaymentMethodDefinitionRequest>>[0];
};

export function PaymentMethodDefinitionModal({
  open,
  editing,
  cashRegisterId,
  confirmLoading,
  onCancel,
  onSubmit,
  form,
}: PaymentMethodDefinitionModalProps) {
  const { t } = useI18n();

  const legacyPaymentMethodOptions = useMemo(
    () => [
      { value: 0, label: t('settings.paymentMethods.form.legacyOption0') },
      { value: 1, label: t('settings.paymentMethods.form.legacyOption1') },
      { value: 2, label: t('settings.paymentMethods.form.legacyOption2') },
      { value: 3, label: t('settings.paymentMethods.form.legacyOption3') },
      { value: 4, label: t('settings.paymentMethods.form.legacyOption4') },
      { value: 5, label: t('settings.paymentMethods.form.legacyOption5') },
    ],
    [t],
  );

  const handleOk = async () => {
    const values = await form.validateFields();
    await onSubmit({
      cashRegisterId,
      code: values.code?.trim() ?? '',
      name: values.name?.trim() ?? '',
      legacyPaymentMethodValue: values.legacyPaymentMethodValue,
      fiscalCategory: values.fiscalCategory?.trim() || null,
      isActive: values.isActive ?? true,
      isDefault: values.isDefault ?? false,
      displayOrder: values.displayOrder ?? 0,
      requiresTerminal: values.requiresTerminal ?? false,
      terminalType: values.terminalType?.trim() || null,
      allowRefund: values.allowRefund ?? true,
      icon: values.icon?.trim() || null,
      metadataJson: values.metadataJson?.trim() || null,
    });
  };

  return (
    <Modal
      title={editing ? t('settings.paymentMethods.editTitle') : t('settings.paymentMethods.createTitle')}
      open={open}
      onCancel={onCancel}
      onOk={handleOk}
      confirmLoading={confirmLoading}
      destroyOnHidden
      width={640}
      okText={t('common.buttons.save')}
      cancelText={t('common.buttons.cancel')}
    >
      <Form form={form} layout="vertical">
        <Form.Item name="cashRegisterId" hidden>
          <Input />
        </Form.Item>
        <Form.Item
          name="code"
          label={t('settings.paymentMethods.form.code')}
          rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
          extra={t('settings.paymentMethods.form.codeHint')}
        >
          <Input disabled={!!editing} autoComplete="off" />
        </Form.Item>
        <Form.Item name="name" label={t('settings.paymentMethods.form.name')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
          <Input />
        </Form.Item>
        <Form.Item name="legacyPaymentMethodValue" label={t('settings.paymentMethods.form.legacy')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
          <Select options={legacyPaymentMethodOptions} />
        </Form.Item>
        <Form.Item name="fiscalCategory" label={t('settings.paymentMethods.form.fiscalCategory')}>
          <Input />
        </Form.Item>
        <Form.Item name="displayOrder" label={t('settings.paymentMethods.form.order')}>
          <InputNumber style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="isActive" label={t('settings.paymentMethods.form.active')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="isDefault" label={t('settings.paymentMethods.form.default')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="requiresTerminal" label={t('settings.paymentMethods.form.requiresTerminal')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="terminalType" label={t('settings.paymentMethods.form.terminalType')}>
          <Input />
        </Form.Item>
        <Form.Item name="allowRefund" label={t('settings.paymentMethods.form.allowRefund')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="icon" label={t('settings.paymentMethods.form.icon')}>
          <Input placeholder={t('settings.paymentMethods.form.iconPlaceholder')} />
        </Form.Item>
        <Form.Item name="metadataJson" label={t('settings.paymentMethods.form.metadata')}>
          <Input.TextArea rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
