'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Form, Input, InputNumber, Modal, Space, Typography } from 'antd';
import React, { useEffect, useState } from 'react';

import { TaxSelect } from '@/components/TaxSelect';
import {
  type PriceChangeResult,
  changeProductPrice,
  priceHistoryQueryKey,
  validatePriceChange,
} from '@/features/tax/api/priceHistory';
import { useNotify } from '@/hooks/useNotify';
import { useTaxGroups } from '@/hooks/useTaxGroups';
import { useI18n } from '@/i18n';

export type PriceChangeModalProps = {
  open: boolean;
  productId: string;
  productName?: string;
  currentPrice: number;
  currentTaxGroupId: string;
  currentTaxRate: number;
  onClose: () => void;
  onSuccess: (result: PriceChangeResult) => void | Promise<void>;
};

type PriceChangeFormValues = {
  newPrice: number;
  newTaxGroupId: string;
  reason: string;
};

export function PriceChangeModal({
  open,
  productId,
  productName,
  currentPrice,
  currentTaxGroupId,
  currentTaxRate,
  onClose,
  onSuccess,
}: PriceChangeModalProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<PriceChangeFormValues>();
  const [submitting, setSubmitting] = useState(false);
  const { data: taxGroups } = useTaxGroups(open);

  const validationQuery = useQuery({
    queryKey: ['price-history', 'validate', productId, currentPrice, currentTaxGroupId],
    enabled: open && !!productId,
    queryFn: () =>
      validatePriceChange({
        productId,
        newPrice: Math.max(currentPrice, 0.01),
        newTaxGroupId: currentTaxGroupId,
        reason: 'validate',
      }),
    staleTime: 30_000,
  });

  const requiresNewVersion = validationQuery.data?.requiresNewProductVersion === true;
  const hasFiscalHistory = validationQuery.data?.hasFiscalHistory === true;

  useEffect(() => {
    if (!open) return;
    form.setFieldsValue({
      newPrice: currentPrice,
      newTaxGroupId: currentTaxGroupId,
      reason: undefined,
    });
  }, [open, currentPrice, currentTaxGroupId, form]);

  const handleFinish = async (values: PriceChangeFormValues) => {
    setSubmitting(true);
    try {
      const result = await changeProductPrice({
        productId,
        newPrice: Number(values.newPrice),
        newTaxGroupId: values.newTaxGroupId,
        reason: values.reason.trim(),
      });

      if (!result.succeeded) {
        notify.error(result.errorMessage || t('products.priceChange.changeFailed'));
        return;
      }

      if (result.createdNewProductVersion) {
        notify.success(t('products.priceChange.versionCreated'));
      } else {
        notify.success(t('products.priceChange.updated'));
      }

      if (result.warningMessage) {
        notify.info(result.warningMessage, { mode: 'notification' });
      }

      await queryClient.invalidateQueries({ queryKey: priceHistoryQueryKey });
      await onSuccess(result);
      onClose();
    } catch (err) {
      notify.apiError(err, {
        logContext: 'PriceChangeModal.change',
        fallbackKey: 'products.priceChange.changeFailed',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const watchedTaxGroupId = Form.useWatch('newTaxGroupId', form);
  const selectedRate =
    taxGroups?.find((g) => g.id === watchedTaxGroupId)?.rate ?? currentTaxRate;

  return (
    <Modal
      title={t('products.priceChange.modalTitle')}
      open={open}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
      width={520}
    >
      {productName ? (
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {productName}
        </Typography.Paragraph>
      ) : null}

      <Alert
        type="info"
        showIcon
        title={t('products.priceChange.infoTitle')}
        description={
          hasFiscalHistory || requiresNewVersion
            ? t('products.priceChange.infoHasReceipts')
            : t('products.priceChange.infoNoReceipts')
        }
        style={{ marginBottom: 16 }}
      />

      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          marginBottom: 16,
          padding: '12px 14px',
          background: 'var(--ant-color-fill-alter, rgba(0,0,0,0.02))',
          borderRadius: 8,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <Typography.Text type="secondary">{t('products.priceChange.currentPrice')}</Typography.Text>
          <Typography.Text strong>
            €{Number(currentPrice).toFixed(2)}
          </Typography.Text>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <Typography.Text type="secondary">{t('products.priceChange.currentTax')}</Typography.Text>
          <Typography.Text strong>{Number(currentTaxRate).toFixed(2)}%</Typography.Text>
        </div>
      </div>

      <Form
        form={form}
        layout="vertical"
        onFinish={(vals) => void handleFinish(vals)}
        initialValues={{
          newPrice: currentPrice,
          newTaxGroupId: currentTaxGroupId,
        }}
      >
        <Form.Item
          name="newPrice"
          label={t('products.priceChange.newPrice')}
          rules={[
            { required: true, message: t('products.priceChange.newPriceRequired') },
            {
              type: 'number',
              min: 0.01,
              message: t('products.priceChange.newPriceMin'),
            },
          ]}
        >
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} precision={2} prefix="€" />
        </Form.Item>

        <Form.Item
          name="newTaxGroupId"
          label={t('products.priceChange.newTaxGroup')}
          rules={[{ required: true, message: t('products.priceChange.newTaxGroupRequired') }]}
          extra={t('products.priceChange.selectedRate', { rate: Number(selectedRate).toFixed(2) })}
        >
          <TaxSelect />
        </Form.Item>

        <Form.Item
          name="reason"
          label={t('products.priceChange.reason')}
          rules={[
            { required: true, message: t('products.priceChange.reasonRequired') },
            { max: 500, message: t('products.form.fieldMaxLength', { max: 500 }) },
          ]}
        >
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>

        {hasFiscalHistory || requiresNewVersion ? (
          <Alert
            type="warning"
            showIcon
            title={t('products.priceChange.rksvHintTitle')}
            description={
              validationQuery.data?.compliance?.warnings?.[0]?.message ||
              t('products.priceChange.rksvHintDescription')
            }
            style={{ marginBottom: 16 }}
          />
        ) : null}

        <Form.Item style={{ marginBottom: 0, marginTop: 8 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {hasFiscalHistory || requiresNewVersion
                ? t('products.priceChange.submitVersion')
                : t('products.priceChange.submitUpdate')}
            </Button>
            <Button onClick={onClose} disabled={submitting}>
              {t('common.buttons.cancel')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Modal>
  );
}
