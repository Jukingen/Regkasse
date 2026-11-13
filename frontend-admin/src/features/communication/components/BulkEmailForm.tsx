'use client';

import { SendOutlined } from '@ant-design/icons';
import { Alert, Button, Form, Input, Select, Space, Spin } from 'antd';
import React, { useEffect, useMemo, useState } from 'react';

import {
  usePostApiAdminCommunicationBulkEmail,
  usePostApiAdminCommunicationBulkEmailPreview,
} from '@/api/generated/admin/admin';
import type { BulkEmailPreviewResult, BulkEmailResult } from '@/api/generated/model';
import { BulkEmailResultModal } from '@/features/communication/components/BulkEmailResultModal';
import { RecipientPreview } from '@/features/communication/components/RecipientPreview';
import {
  BULK_EMAIL_LICENSE_OPTIONS,
  BULK_EMAIL_STATUS_LABEL_KEYS,
  BULK_EMAIL_STATUS_OPTIONS,
  type BulkEmailFormValues,
  isBulkEmailFormValid,
  toBulkEmailRequest,
} from '@/features/communication/utils/bulkEmailValidation';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
export function BulkEmailForm() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const [form] = Form.useForm<BulkEmailFormValues>();
  const [preview, setPreview] = useState<BulkEmailPreviewResult | null>(null);
  const [previewChecked, setPreviewChecked] = useState(false);
  const [previewFailed, setPreviewFailed] = useState(false);
  const [resultOpen, setResultOpen] = useState(false);
  const [result, setResult] = useState<BulkEmailResult | null>(null);

  const filterByStatus = Form.useWatch('filterByStatus', form);
  const filterByLicenseType = Form.useWatch('filterByLicenseType', form);
  const tenantIds = Form.useWatch('tenantIds', form);

  const tenantsQuery = useGetApiAdminTenants();
  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? [])
        .filter((row) => row.isActive && isBusinessTenantSlug(row.slug))
        .sort((a, b) => a.name.localeCompare(b.name))
        .map((tenant) => ({
          value: tenant.id,
          label: `${tenant.name} (${tenant.slug})`,
        })),
    [tenantsQuery.data]
  );

  const previewMutation = usePostApiAdminCommunicationBulkEmailPreview({
    mutation: {
      onSuccess: (data) => {
        setPreview(data);
        setPreviewChecked(true);
        setPreviewFailed(false);
      },
      onError: () => {
        setPreview(null);
        setPreviewChecked(true);
        setPreviewFailed(true);
        notify.errorKey('communication.bulkEmail.previewError');
      },
    },
  });

  const sendMutation = usePostApiAdminCommunicationBulkEmail({
    mutation: {
      onSuccess: (data) => {
        setResult(data);
        setResultOpen(true);
        notify.successKey('communication.bulkEmail.success');
      },
      onError: (err: unknown) => {
        const status = (err as { response?: { status?: number } })?.response?.status;
        notify.errorKey(
          status === 429
            ? 'communication.bulkEmail.rateLimitWarning'
            : 'communication.bulkEmail.error'
        );
      },
    },
  });

  // Reset stale preview when filters change.
  useEffect(() => {
    setPreview(null);
    setPreviewChecked(false);
    setPreviewFailed(false);
  }, [filterByStatus, filterByLicenseType, tenantIds]);

  const runPreview = () => {
    const values = form.getFieldsValue();
    previewMutation.mutate({
      data: {
        filterByStatus: values.filterByStatus,
        filterByLicenseType: values.filterByLicenseType,
        tenantIds: values.tenantIds?.length ? values.tenantIds : null,
      },
    });
  };

  const onSend = async () => {
    let values: BulkEmailFormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }
    if (!isBulkEmailFormValid(values)) {
      return;
    }

    let recipientCount = preview?.recipientCount ?? 0;
    if (!previewChecked || previewFailed) {
      try {
        const data = await previewMutation.mutateAsync({
          data: {
            filterByStatus: values.filterByStatus,
            filterByLicenseType: values.filterByLicenseType,
            tenantIds: values.tenantIds?.length ? values.tenantIds : null,
          },
        });
        recipientCount = data.recipientCount ?? 0;
      } catch {
        return;
      }
    }

    if (recipientCount <= 0) {
      notify.errorKey('communication.bulkEmail.noRecipients');
      return;
    }

    modal.confirm({
      title: t('communication.bulkEmail.confirmSend'),
      content: t('communication.bulkEmail.confirmMessage', { count: recipientCount }),
      okText: t('communication.bulkEmail.send'),
      onOk: () => sendMutation.mutateAsync({ data: toBulkEmailRequest(values) }),
    });
  };

  const sending = sendMutation.isPending;

  return (
    <>
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('communication.bulkEmail.rateLimitWarning')}
      />

      <Spin spinning={sending} description={t('communication.bulkEmail.sending')}>
        <Form form={form} layout="vertical" initialValues={{ body: '', tenantIds: [] }}>
          <Space wrap style={{ width: '100%', marginBottom: 8 }} size="middle" align="start">
            <Form.Item
              name="filterByStatus"
              label={t('communication.bulkEmail.filterByStatus')}
              style={{ marginBottom: 0 }}
            >
              <Select
                allowClear
                placeholder={t('communication.bulkEmail.allStatuses')}
                style={{ width: 200 }}
                options={BULK_EMAIL_STATUS_OPTIONS.map((value) => ({
                  value,
                  label: t(BULK_EMAIL_STATUS_LABEL_KEYS[value]),
                }))}
              />
            </Form.Item>
            <Form.Item
              name="filterByLicenseType"
              label={t('communication.bulkEmail.filterByLicenseType')}
              style={{ marginBottom: 0 }}
            >
              <Select
                allowClear
                placeholder={t('communication.bulkEmail.allLicenseTypes')}
                style={{ width: 200 }}
                options={BULK_EMAIL_LICENSE_OPTIONS.map((value) => ({
                  value,
                  label: value,
                }))}
              />
            </Form.Item>
            <Form.Item
              name="tenantIds"
              label={t('communication.bulkEmail.tenantIds')}
              extra={t('communication.bulkEmail.tenantIdsHelp')}
              style={{ marginBottom: 0, minWidth: 280 }}
            >
              <Select
                mode="multiple"
                allowClear
                showSearch
                optionFilterProp="label"
                placeholder={t('communication.bulkEmail.tenantIds')}
                style={{ minWidth: 280 }}
                loading={tenantsQuery.isLoading}
                options={tenantOptions}
              />
            </Form.Item>
          </Space>

          <Space wrap style={{ marginBottom: 16 }} size="middle">
            <Button
              onClick={runPreview}
              loading={previewMutation.isPending}
              data-testid="bulk-email-preview"
            >
              {t('communication.bulkEmail.preview')}
            </Button>
            <RecipientPreview
              checked={previewChecked}
              loading={previewMutation.isPending}
              error={previewFailed}
              recipientCount={preview?.recipientCount}
              tenantCount={preview?.tenantCount}
            />
          </Space>

          <Form.Item
            name="subject"
            label={t('communication.bulkEmail.subject')}
            rules={[
              { required: true, message: t('communication.bulkEmail.subjectRequired') },
              { whitespace: true, message: t('communication.bulkEmail.subjectRequired') },
            ]}
          >
            <Input maxLength={500} />
          </Form.Item>

          <Form.Item
            name="body"
            label={t('communication.bulkEmail.body')}
            rules={[
              { required: true, message: t('communication.bulkEmail.bodyRequired') },
              {
                validator: async (_, value: string | undefined) => {
                  if (!value || !value.replace(/<[^>]*>/g, '').trim()) {
                    return Promise.reject(new Error(t('communication.bulkEmail.bodyRequired')));
                  }
                },
              },
            ]}
            extra={t('communication.bulkEmail.listHelp')}
          >
            <Input.TextArea
              rows={12}
              placeholder={t('communication.bulkEmail.bodyPlaceholder')}
              maxLength={100000}
            />
          </Form.Item>

          <Button
            type="primary"
            icon={<SendOutlined />}
            loading={sending}
            onClick={() => void onSend()}
            data-testid="bulk-email-send"
          >
            {sending ? t('communication.bulkEmail.sending') : t('communication.bulkEmail.send')}
          </Button>
        </Form>
      </Spin>

      <BulkEmailResultModal
        open={resultOpen}
        result={result}
        onClose={() => setResultOpen(false)}
      />
    </>
  );
}
