'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, Typography } from 'antd';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type LieferandoCredentialStatus = {
  providerEnabled: boolean;
  credentialStatus: 'configured' | 'missing' | string;
};

type LieferandoCredentialForm = {
  clientId?: string;
  clientSecret?: string;
  chainId?: string;
  webhookToken?: string;
};

const queryKey = ['online-order-integration-lieferando-credentials'] as const;

export function LieferandoCredentialsSection() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const [form] = Form.useForm<LieferandoCredentialForm>();
  const canEdit = hasPermission(PERMISSIONS.DIGITAL_ORDERS_MANAGE) && !isSuperAdmin;

  const status = useQuery({
    queryKey,
    enabled: canEdit,
    queryFn: () =>
      customInstance<LieferandoCredentialStatus>({
        url: '/api/admin/online-order-integrations/lieferando/credentials',
        method: 'GET',
      }),
  });

  const save = useMutation({
    mutationFn: (values: LieferandoCredentialForm) =>
      customInstance<{ id: string }>({
        url: '/api/admin/online-order-integrations/lieferando/credentials',
        method: 'PUT',
        data: {
          clientId: values.clientId?.trim() ?? '',
          clientSecret: values.clientSecret?.trim() ?? '',
          chainId: values.chainId?.trim() ?? '',
          webhookToken: values.webhookToken?.trim() ?? '',
        },
      }),
    onSuccess: async () => {
      notify.successKey('onlineOrders.providers.lieferandoSaved');
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'OnlineOrderProviders.lieferandoCredentials',
        fallbackKey: 'onlineOrders.providers.lieferandoSaveFailed',
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
      <Typography.Title level={4}>{t('onlineOrders.providers.lieferandoTenantTitle')}</Typography.Title>
      <Typography.Paragraph>
        {status.isSuccess
          ? (providerEnabled ? credentialStatus : t('onlineOrders.providers.disabled'))
          : null}
      </Typography.Paragraph>
      <Form<LieferandoCredentialForm>
        name="lieferandoCredentials"
        form={form}
        layout="vertical"
        onFinish={(values) => save.mutate(values)}
      >
        <Form.Item name="clientId" label={t('onlineOrders.providers.lieferandoAuthorizationKey')}>
          <Input autoComplete="off" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="clientSecret" label={t('onlineOrders.providers.lieferandoHmacSecret')}>
          <Input.Password autoComplete="new-password" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="webhookToken" label={t('onlineOrders.providers.lieferandoFlytApiKey')}>
          <Input.Password autoComplete="new-password" disabled={!providerEnabled} />
        </Form.Item>
        <Form.Item name="chainId" label={t('onlineOrders.providers.lieferandoBrandId')}>
          <Input autoComplete="off" disabled={!providerEnabled} />
        </Form.Item>
        <Button type="primary" htmlType="submit" disabled={!providerEnabled} loading={save.isPending}>
          {t('onlineOrders.providers.lieferandoSave')}
        </Button>
      </Form>
    </section>
  );
}
