'use client';

import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd';
import { useEffect } from 'react';

import { FormSkeleton } from '@/components/Skeleton';
import { DEFAULT_THANK_YOU_MESSAGE } from '@/features/settings/api/receiptSettingsApi';
import {
  useReceiptSettings,
  useUpdateReceiptSettings,
} from '@/features/settings/hooks/useReceiptSettings';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type ReceiptSettingsFormValues = {
  thankYouMessage: string;
};

export function ReceiptSettings() {
  const { t } = useI18n();
  const notify = useNotify();
  const [form] = Form.useForm<ReceiptSettingsFormValues>();
  const { data, isLoading, isError, refetch, isFetching } = useReceiptSettings();
  const updateSettings = useUpdateReceiptSettings();
  const watchedMessage = Form.useWatch('thankYouMessage', form);
  const previewMessage = (watchedMessage ?? '').trim() || DEFAULT_THANK_YOU_MESSAGE;

  useEffect(() => {
    if (data) {
      form.setFieldsValue({ thankYouMessage: data.thankYouMessage });
    }
  }, [data, form]);

  const onFinish = async (values: ReceiptSettingsFormValues) => {
    try {
      await updateSettings.mutateAsync(values.thankYouMessage ?? '');
      notify.successKey('settings.receiptPage.saveSuccess');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'ReceiptSettings.save',
        fallbackKey: 'settings.page.saveFailed',
      });
    }
  };

  if (isLoading) {
    return <FormSkeleton fields={3} loading />;
  }

  if (isError) {
    return (
      <Alert
        type="error"
        showIcon
        title={t('settings.page.loadErrorTitle')}
        description={t('settings.page.loadErrorFallback')}
        action={
          <Button size="small" type="primary" onClick={() => void refetch()} loading={isFetching}>
            {t('common.buttons.retry')}
          </Button>
        }
      />
    );
  }

  return (
    <Card title={t('settings.receiptPage.cardTitle')}>
      <Alert
        type="info"
        showIcon
        title={t('settings.receiptPage.infoTitle')}
        description={t('settings.receiptPage.infoDescription')}
        style={{ marginBottom: 24 }}
      />

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => void onFinish(values)}
        initialValues={{ thankYouMessage: data?.thankYouMessage ?? '' }}
      >
        <Form.Item
          name="thankYouMessage"
          label={t('settings.receiptPage.thankYouLabel')}
          extra={t('settings.receiptPage.thankYouHint')}
          rules={[{ max: 500, message: t('settings.receiptPage.thankYouMax') }]}
        >
          <Input.TextArea
            rows={3}
            maxLength={500}
            showCount
            placeholder={DEFAULT_THANK_YOU_MESSAGE}
          />
        </Form.Item>

        <Typography.Title level={5}>{t('settings.receiptPage.previewTitle')}</Typography.Title>
        <div
          style={{
            fontFamily: '"Courier New", Courier, monospace',
            fontSize: 12,
            background: '#fafafa',
            border: '1px dashed #d9d9d9',
            borderRadius: 8,
            padding: 16,
            maxWidth: 320,
            marginBottom: 24,
            color: '#000',
          }}
        >
          <div style={{ textAlign: 'center', fontWeight: 700, marginBottom: 8 }}>
            {t('settings.receiptPage.previewCompany')}
          </div>
          <div>Kassen-ID: KASSE-001</div>
          <div>{t('settings.receiptPage.previewKassierer')}</div>
          <div style={{ borderTop: '1px dashed #000', margin: '8px 0' }} />
          <div style={{ fontWeight: 700 }}>RKSV-konform</div>
          <div style={{ marginTop: 8, textAlign: 'center' }}>{previewMessage}</div>
          {data?.companyDescription?.trim() &&
          data.companyDescription.trim() !== previewMessage ? (
            <div style={{ marginTop: 4, textAlign: 'center' }}>{data.companyDescription}</div>
          ) : null}
        </div>

        <Form.Item>
          <Space wrap>
            <Button type="primary" htmlType="submit" loading={updateSettings.isPending}>
              {t('settings.page.saveChanges')}
            </Button>
            <Button
              onClick={() => {
                form.setFieldsValue({ thankYouMessage: data?.thankYouMessage ?? '' });
              }}
            >
              {t('settings.receiptPage.reset')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  );
}
