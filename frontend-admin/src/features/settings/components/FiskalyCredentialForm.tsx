'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, Select, Typography } from 'antd';
import { useEffect } from 'react';

import {
  getFiskalyCredentials,
  updateFiskalyCredentials,
} from '@/features/dashboard/api/fiskalyStatus';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

const CREDENTIALS_QUERY_KEY = ['admin', 'fiskaly', 'credentials'] as const;

type FiskalyCredentialFormValues = {
  apiKey?: string;
  apiSecret?: string;
  scuId?: string;
  environment?: string;
};

/** BYOK Fiskaly API key, secret, and SCU for the ambient tenant. The secret is write-only. */
export function FiskalyCredentialForm() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { canManageCashRegisters } = usePermissions();
  const [form] = Form.useForm<FiskalyCredentialFormValues>();

  const query = useQuery({
    queryKey: CREDENTIALS_QUERY_KEY,
    queryFn: ({ signal }) => getFiskalyCredentials(signal),
    enabled: canManageCashRegisters,
  });

  useEffect(() => {
    if (!query.data) {
      return;
    }
    form.setFieldsValue({
      apiKey: '',
      apiSecret: '',
      scuId: query.data.scuId ?? '',
      environment: query.data.environment === 'LIVE' ? 'LIVE' : 'TEST',
    });
  }, [form, query.data]);

  const mutation = useMutation({
    mutationFn: (values: FiskalyCredentialFormValues) =>
      updateFiskalyCredentials({
        apiKey: values.apiKey?.trim() ?? '',
        apiSecret: values.apiSecret?.trim() ?? '',
        scuId: values.scuId?.trim() ?? '',
        environment: values.environment === 'LIVE' ? 'LIVE' : 'TEST',
      }),
    onSuccess: (data) => {
      form.setFieldsValue({ apiKey: '', apiSecret: '' });
      queryClient.setQueryData(CREDENTIALS_QUERY_KEY, data);
      notify.successKey('settings.manager.fiscal.credentials.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalyCredentials.update',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  if (!canManageCashRegisters) {
    return null;
  }

  const hint = query.data?.configured ? query.data.apiKeyHint : null;

  return (
    <Form<FiskalyCredentialFormValues>
      form={form}
      layout="vertical"
      style={{ marginTop: 16 }}
      initialValues={{ apiKey: '', apiSecret: '', scuId: '', environment: 'TEST' }}
      onFinish={(values) => mutation.mutate(values)}
    >
      <Form.Item name="apiKey" label={t('settings.manager.fiscal.credentials.apiKey')}>
        <Input autoComplete="off" />
      </Form.Item>
      {hint ? (
        <Typography.Text type="secondary">
          {t('settings.manager.fiscal.credentials.hint', { hint })}
        </Typography.Text>
      ) : (
        <Typography.Text type="secondary">
          {t('settings.manager.fiscal.credentials.missing')}
        </Typography.Text>
      )}
      <Form.Item name="apiSecret" label={t('settings.manager.fiscal.credentials.apiSecret')}>
        <Input.Password
          autoComplete="new-password"
          placeholder={
            query.data?.configured
              ? t('settings.manager.fiscal.credentials.secretPlaceholder')
              : undefined
          }
        />
      </Form.Item>
      <Form.Item name="scuId" label={t('settings.manager.fiscal.credentials.scuId')}>
        <Input autoComplete="off" />
      </Form.Item>
      <Form.Item name="environment" label={t('settings.manager.fiscal.credentials.environment')}>
        <Select
          options={[
            { value: 'TEST', label: t('settings.manager.fiscal.credentials.envTest') },
            { value: 'LIVE', label: t('settings.manager.fiscal.credentials.envLive') },
          ]}
          style={{ maxWidth: 240 }}
        />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={mutation.isPending}>
        {t('settings.manager.fiscal.credentials.save')}
      </Button>
    </Form>
  );
}
