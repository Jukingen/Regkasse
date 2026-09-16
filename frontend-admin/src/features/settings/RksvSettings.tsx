'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, InputNumber, Select, Space, Switch, Typography } from 'antd';
import Link from 'next/link';
import { useEffect } from 'react';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

import {
  fetchMonatsbelegPolicy,
  monatsbelegPolicyQueryKey,
  putMonatsbelegPolicy,
  type MonatsbelegBlockingMode,
} from './api/monatsbelegPolicy';

type FormValues = {
  blockingMode: MonatsbelegBlockingMode;
  autoMonatsbelegEnabled: boolean;
  monatsbelegRetryCount: number;
};

export function RksvSettings() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<FormValues>();

  const query = useQuery({
    queryKey: monatsbelegPolicyQueryKey,
    queryFn: ({ signal }) => fetchMonatsbelegPolicy(signal),
  });

  const mutation = useMutation({
    mutationFn: putMonatsbelegPolicy,
    onSuccess: async (dto) => {
      form.setFieldsValue({
        blockingMode: dto.blockingMode,
        autoMonatsbelegEnabled: dto.autoMonatsbelegEnabled,
        monatsbelegRetryCount: dto.monatsbelegRetryCount,
      });
      await queryClient.invalidateQueries({ queryKey: monatsbelegPolicyQueryKey });
      notify.successKey('settings.rksv.monatsbelegPolicy.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'RksvSettings.save',
        fallbackKey: 'settings.rksv.monatsbelegPolicy.saveFailed',
      });
    },
  });

  useEffect(() => {
    if (!query.data) return;
    form.setFieldsValue({
      blockingMode: query.data.blockingMode,
      autoMonatsbelegEnabled: query.data.autoMonatsbelegEnabled,
      monatsbelegRetryCount: query.data.monatsbelegRetryCount,
    });
  }, [form, query.data]);

  if (query.isError) {
    return (
      <Card title={t('settings.rksv.monatsbelegPolicy.title')}>
        <Alert
          type="error"
          showIcon
          title={t('settings.page.loadErrorTitle')}
          description={t('settings.rksv.monatsbelegPolicy.loadFailed')}
          action={
            <Button size="small" type="primary" onClick={() => void query.refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </Card>
    );
  }

  return (
    <Card title={t('settings.rksv.monatsbelegPolicy.title')} loading={query.isLoading}>
      <Typography.Paragraph type="secondary">
        {t('settings.rksv.monatsbelegPolicy.intro')}
      </Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        disabled={!canEdit || mutation.isPending}
        onFinish={(values) => {
          mutation.mutate({
            blockingMode: values.blockingMode,
            autoMonatsbelegEnabled: values.autoMonatsbelegEnabled,
            monatsbelegRetryCount: values.monatsbelegRetryCount,
          });
        }}
      >
        <Form.Item
          name="blockingMode"
          label={t('settings.rksv.monatsbelegPolicy.modeLabel')}
          extra={t('settings.rksv.monatsbelegPolicy.modeHelp')}
          rules={[{ required: true }]}
        >
          <Select
            options={[
              {
                value: 'Strict',
                label: t('settings.rksv.monatsbelegPolicy.modeStrict'),
              },
              {
                value: 'GracePeriod',
                label: t('settings.rksv.monatsbelegPolicy.modeGracePeriod'),
              },
              {
                value: 'WarningOnly',
                label: t('settings.rksv.monatsbelegPolicy.modeWarningOnly'),
              },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="autoMonatsbelegEnabled"
          label={t('settings.rksv.monatsbelegPolicy.autoLabel')}
          extra={t('settings.rksv.monatsbelegPolicy.autoHelp')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
        <Form.Item
          name="monatsbelegRetryCount"
          label={t('settings.rksv.monatsbelegPolicy.retryLabel')}
          extra={t('settings.rksv.monatsbelegPolicy.retryHelp')}
          rules={[{ required: true }]}
        >
          <InputNumber min={1} max={5} precision={0} />
        </Form.Item>
        <Space wrap>
          <Button type="primary" htmlType="submit" loading={mutation.isPending} disabled={!canEdit}>
            {t('common.buttons.save')}
          </Button>
          <Link href="/rksv/monatsbelege">{t('settings.rksv.monatsbelegPolicy.openList')}</Link>
          <Link href="/rksv/sonderbelege?focus=monatsbeleg">
            {t('settings.rksv.monatsbelegPolicy.openCreate')}
          </Link>
        </Space>
      </Form>
      {!canEdit ? (
        <Alert
          type="info"
          showIcon
          style={{ marginTop: 12 }}
          title={t('settings.manager.readOnly.title')}
          description={t('settings.rksv.monatsbelegPolicy.readOnly')}
        />
      ) : null}
    </Card>
  );
}
