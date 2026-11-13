'use client';

import { Alert, Checkbox, Descriptions, Form, Input, Modal, Typography } from 'antd';
import dayjs from 'dayjs';
import React, { useEffect, useMemo } from 'react';

import type { TrialConversionResult, TrialTenantSummary } from '@/features/super-admin/api/adminTrials';
import { useI18n } from '@/i18n';

export type ConvertTrialFormValues = {
  licenseSaleId: string;
  addRemainingTrialDays: boolean;
  notes?: string;
};

export type ConvertTrialToPaidModalProps = {
  open: boolean;
  tenant: TrialTenantSummary | null;
  loading?: boolean;
  onCancel: () => void;
  onSubmit: (values: ConvertTrialFormValues) => void;
  success?: TrialConversionResult | null;
  onSuccessClose?: () => void;
};

function estimateRemainingDays(endsAt?: string | null): number {
  if (!endsAt) return 0;
  const ends = dayjs(endsAt);
  const days = Math.ceil(ends.diff(dayjs(), 'day', true));
  return Math.max(0, days);
}

export function ConvertTrialToPaidModal({
  open,
  tenant,
  loading,
  onCancel,
  onSubmit,
  success,
  onSuccessClose,
}: ConvertTrialToPaidModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<ConvertTrialFormValues>();
  const remaining = useMemo(
    () => estimateRemainingDays(tenant?.trialEndsAtUtc),
    [tenant?.trialEndsAtUtc]
  );

  useEffect(() => {
    if (open && !success) {
      form.setFieldsValue({
        licenseSaleId: '',
        addRemainingTrialDays: true,
        notes: undefined,
      });
    }
  }, [open, success, form]);

  if (success) {
    return (
      <Modal
        title={t('trials.convert.successTitle')}
        open={open}
        onCancel={onSuccessClose}
        onOk={onSuccessClose}
        okText={t('common.buttons.close')}
        cancelButtonProps={{ style: { display: 'none' } }}
        destroyOnHidden
      >
        <Alert
          type="success"
          showIcon
          title={t('trials.convert.successMessage')}
          description={
            <Typography.Paragraph style={{ marginBottom: 0 }}>
              {t('trials.convert.successValidUntil', {
                date: dayjs(success.licenseValidUntilUtc).format('YYYY-MM-DD'),
              })}
              {success.remainingTrialDaysAdded > 0
                ? ` (+${success.remainingTrialDaysAdded} ${t('trials.convert.daysAddedSuffix')})`
                : ''}
            </Typography.Paragraph>
          }
        />
      </Modal>
    );
  }

  return (
    <Modal
      title={t('trials.convert.title')}
      open={open}
      onCancel={onCancel}
      confirmLoading={loading}
      onOk={() => form.submit()}
      okText={t('trials.convert.ok')}
      destroyOnHidden
      width={560}
    >
      {tenant ? (
        <Descriptions size="small" column={1} style={{ marginBottom: 16 }} bordered>
          <Descriptions.Item label={t('trials.columns.tenant')}>
            {tenant.name} ({tenant.slug})
          </Descriptions.Item>
          <Descriptions.Item label={t('trials.detail.started')}>
            {tenant.trialStartedAtUtc
              ? dayjs(tenant.trialStartedAtUtc).format('YYYY-MM-DD')
              : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('trials.detail.ends')}>
            {tenant.trialEndsAtUtc ? dayjs(tenant.trialEndsAtUtc).format('YYYY-MM-DD') : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('trials.columns.daysLeft')}>{remaining}</Descriptions.Item>
        </Descriptions>
      ) : null}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ addRemainingTrialDays: true }}
        onFinish={onSubmit}
      >
        <Form.Item
          name="licenseSaleId"
          label={t('trials.convert.saleIdLabel')}
          rules={[{ required: true, message: t('trials.convert.saleIdRequired') }]}
        >
          <Input placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
        </Form.Item>
        <Form.Item name="addRemainingTrialDays" valuePropName="checked">
          <Checkbox>
            {t('trials.convert.addRemainingDays', { count: remaining })}
          </Checkbox>
        </Form.Item>
        <Form.Item name="notes" label={t('trials.convert.notes')}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
