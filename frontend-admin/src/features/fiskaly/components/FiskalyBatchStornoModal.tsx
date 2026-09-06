'use client';

import { Alert, Form, Input, Modal, Typography } from 'antd';
import { useEffect } from 'react';

import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import { useI18n } from '@/i18n';

type Props = {
  open: boolean;
  receipts: ReceiptListItemDto[];
  maxItems: number;
  warnAtItems: number;
  confirmLoading?: boolean;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
};

export function FiskalyBatchStornoModal({
  open,
  receipts,
  maxItems,
  warnAtItems,
  confirmLoading,
  onCancel,
  onConfirm,
}: Props) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ reason: string }>();
  const tooLarge = receipts.length > maxItems;
  const large = receipts.length >= warnAtItems;
  const preview = receipts.slice(0, 8);
  const extra = receipts.length - preview.length;

  useEffect(() => {
    if (open) form.resetFields();
  }, [open, form]);

  return (
    <Modal
      open={open}
      title={t('tseFiskaly.batch.stornoConfirmTitle')}
      okText={t('tseFiskaly.batch.stornoConfirmOk')}
      cancelText={t('tseFiskaly.operations.cancelAction')}
      okButtonProps={{ danger: true, disabled: tooLarge || receipts.length === 0 }}
      confirmLoading={confirmLoading}
      onCancel={onCancel}
      onOk={async () => {
        const values = await form.validateFields();
        onConfirm(values.reason.trim());
      }}
      destroyOnHidden
      width={560}
    >
      {tooLarge ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('tseFiskaly.batch.tooLarge', { max: maxItems })}
        />
      ) : large ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('tseFiskaly.batch.largeWarning', { count: receipts.length, max: maxItems })}
        />
      ) : null}
      <Typography.Paragraph>
        {t('tseFiskaly.batch.stornoConfirmBody', { count: receipts.length })}
      </Typography.Paragraph>
      <ul>
        {preview.map((row) => (
          <li key={row.receiptId}>{row.receiptNumber || row.paymentId}</li>
        ))}
      </ul>
      {extra > 0 ? (
        <Typography.Paragraph type="secondary">
          {t('tseFiskaly.batch.andMore', { count: extra })}
        </Typography.Paragraph>
      ) : null}
      <Form form={form} layout="vertical">
        <Form.Item
          name="reason"
          label={t('tseFiskaly.operations.reasonLabel')}
          rules={[{ required: true, min: 5, message: t('tseFiskaly.operations.reasonRequired') }]}
        >
          <Input.TextArea rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
