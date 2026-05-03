'use client';

import React, { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  Select,
  Space,
  Typography,
  message,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useCreateAdminVoucher, type CreateAdminVoucherResponse } from '@/api/admin/vouchers';
dayjs.extend(utc);

export default function AdminVoucherCreatePage() {
  const { t } = useI18n();
  const router = useRouter();
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission(PERMISSIONS.VOUCHER_CREATE);
  const [form] = Form.useForm();
  const createMutation = useCreateAdminVoucher();
  const [success, setSuccess] = useState<CreateAdminVoucherResponse | null>(null);

  const expiryMode = Form.useWatch('expiryMode', form) as string | undefined;

  if (!canCreate) {
    return (
      <AdminPageShell>
        <Alert type="error" message={t('vouchers.create.permissionDenied')} showIcon />
      </AdminPageShell>
    );
  }

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const mode = values.expiryMode as 'DefaultOneYear' | 'Custom';
      let expiresAtUtc: string | null | undefined;
      if (mode === 'Custom') {
        const d = values.expiresAt as Dayjs;
        expiresAtUtc = dayjs
          .utc(`${d.format('YYYY-MM-DD')}T23:59:59.000Z`)
          .toISOString();
      }
      const res = await createMutation.mutateAsync({
        initialAmount: Number(values.initialAmount),
        currency: (values.currency as string) ?? 'EUR',
        expiryMode: mode,
        expiresAtUtc: mode === 'Custom' ? expiresAtUtc : undefined,
        note: values.note?.trim() ? String(values.note).trim() : null,
      });
      setSuccess(res);
    } catch {
      void message.error(t('vouchers.errors.createFailed'));
    }
  };

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('vouchers.create.heading')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('vouchers.title'), href: '/vouchers' },
          { title: t('vouchers.create.heading') },
        ]}
      />
      <Card>
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            initialAmount: 25,
            currency: 'EUR',
            expiryMode: 'DefaultOneYear',
            note: '',
          }}
        >
          <Form.Item
            name="initialAmount"
            label={t('vouchers.create.amount')}
            rules={[{ required: true, type: 'number', min: 0.01 }]}
          >
            <InputNumber min={0.01} step={0.01} style={{ width: '100%', maxWidth: 320 }} />
          </Form.Item>
          <Form.Item name="currency" label={t('vouchers.create.currency')} rules={[{ required: true }]}>
            <Select
              options={[{ value: 'EUR', label: 'EUR' }]}
              style={{ maxWidth: 200 }}
            />
          </Form.Item>
          <Form.Item name="expiryMode" label={t('vouchers.create.expiryMode')} rules={[{ required: true }]}>
            <Radio.Group>
              <Radio value="DefaultOneYear">{t('vouchers.create.expiryDefaultYear')}</Radio>
              <Radio value="Custom">{t('vouchers.create.expiryCustom')}</Radio>
            </Radio.Group>
          </Form.Item>
          {expiryMode === 'Custom' ? (
            <Form.Item
              name="expiresAt"
              label={t('vouchers.create.expiresAt')}
              rules={[{ required: true, message: t('vouchers.errors.createFailed') }]}
            >
              <DatePicker style={{ width: '100%', maxWidth: 320 }} />
            </Form.Item>
          ) : null}
          <Form.Item name="note" label={t('vouchers.create.note')}>
            <Input.TextArea rows={3} maxLength={500} showCount />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" onClick={handleSubmit} loading={createMutation.isPending}>
                {t('vouchers.create.submit')}
              </Button>
              <Link href="/vouchers">
                <Button>{t('vouchers.detail.back')}</Button>
              </Link>
            </Space>
          </Form.Item>
        </Form>
      </Card>

      <Modal
        open={!!success}
        title={t('vouchers.create.successTitle')}
        onCancel={() => setSuccess(null)}
        footer={[
          <Button key="list" onClick={() => router.push('/vouchers')}>
            {t('vouchers.create.goToList')}
          </Button>,
          <Button
            key="detail"
            type="primary"
            onClick={() => success && router.push(`/vouchers/${success.id}`)}
            disabled={!success}
          >
            {t('vouchers.create.viewDetail')}
          </Button>,
        ]}
      >
        <Alert type="warning" message={t('vouchers.create.plaintextWarning')} showIcon style={{ marginBottom: 16 }} />
        <Typography.Paragraph strong>{t('vouchers.create.plaintextLabel')}</Typography.Paragraph>
        <Typography.Paragraph code copyable={{ text: success?.plaintextCode }}>
          {success?.plaintextCode}
        </Typography.Paragraph>
      </Modal>
    </AdminPageShell>
  );
}
