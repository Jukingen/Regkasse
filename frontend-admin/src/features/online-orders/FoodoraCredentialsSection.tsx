'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, Typography } from 'antd';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type FoodoraCredentialStatus = {
  providerEnabled: boolean;
  credentialStatus: 'configured' | 'missing' | string;
};

type FoodoraCredentialForm = {
  clientId?: string;
  clientSecret?: string;
  chainId?: string;
  webhookToken?: string;
};

const queryKey = ['online-order-integration-foodora-credentials'] as const;

export function FoodoraCredentialsSection() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const [form] = Form.useForm<FoodoraCredentialForm>();
  const canEdit = hasPermission(PERMISSIONS.DIGITAL_ORDERS_MANAGE) && !isSuperAdmin;

  const status = useQuery({
    queryKey,
    enabled: canEdit,
    queryFn: () =>
      customInstance<FoodoraCredentialStatus>({
        url: '/api/admin/online-order-integrations/foodora/credentials',
        method: 'GET',
      }),
  });

  const save = useMutation({
    mutationFn: (values: FoodoraCredentialForm) =>
      customInstance<{ id: string }>({
        url: '/api/admin/online-order-integrations/foodora/credentials',
        method: 'PUT',
        data: {
          clientId: values.clientId?.trim() ?? '',
          clientSecret: values.clientSecret?.trim() ?? '',
          chainId: values.chainId?.trim() ?? '',
          webhookToken: values.webhookToken?.trim() ?? '',
        },
      }),
    onSuccess: async () => {
      notify.successKey('onlineOrders.providers.foodoraSaved');
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'OnlineOrderProviders.foodoraCredentials',
        fallbackKey: 'onlineOrders.providers.foodoraSaveFailed',
      });
    },
  });

  if (!canEdit) return null;

  const providerEnabled = status.data?.providerEnabled === true;
  const credentialStatus = status.data?.credentialStatus === 'configured'
    ? t('onlineOrders.providers.statusConfigured')
    : t('onlineOrders.providers.statusMissing');

  return (
    <section>
      <Typography.Title level={4}>{t('onlineOrders.providers.foodoraTenantTitle')}</Typography.Title>
      <Typography.Paragraph>
        {providerEnabled ? credentialStatus : t('onlineOrders.providers.disabled')}
      </Typography.Paragraph>
      <Form<FoodoraCredentialForm> form={form} layout="vertical" onFinish={(values) => save.mutate(values)}>
        <Form.Item name="clientId" label={t('onlineOrders.providers.foodoraClientId')}>
          <Input autoComplete="off" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="clientSecret" label={t('onlineOrders.providers.foodoraClientSecret')}>
          <Input.Password autoComplete="new-password" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="chainId" label={t('onlineOrders.providers.foodoraChainId')}>
          <Input autoComplete="off" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="webhookToken" label={t('onlineOrders.providers.foodoraWebhookToken')}>
          <Input.Password autoComplete="new-password" disabled={!providerEnabled} />
        </Form.Item>
        <Button type="primary" htmlType="submit" disabled={!providerEnabled} loading={save.isPending}>
          {t('onlineOrders.providers.foodoraSave')}
        </Button>
      </Form>
    </section>
  );
}
