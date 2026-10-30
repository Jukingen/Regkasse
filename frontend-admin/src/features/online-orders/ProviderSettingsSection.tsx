'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, Switch, Typography } from 'antd';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type ProviderAdmin = {
  provider: string;
  enabled: boolean;
  credentialStatus: 'configured' | 'missing' | string;
};

type ProviderForm = {
  enabled: boolean;
  clientId?: string;
  clientSecret?: string;
};

const woltQueryKey = ['online-order-integration-provider', 'wolt'] as const;
const foodoraQueryKey = ['online-order-integration-provider', 'foodora'] as const;

export function ProviderSettingsSection() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const [form] = Form.useForm<ProviderForm>();
  const canConfigure = hasPermission(PERMISSIONS.DIGITAL_MANAGE);

  const status = useQuery({
    queryKey: woltQueryKey,
    enabled: canConfigure,
    queryFn: () =>
      customInstance<ProviderAdmin>({
        url: '/api/admin/online-order-integrations/providers',
        method: 'GET',
        params: { provider: 'wolt' },
      }),
  });

  const foodora = useQuery({
    queryKey: foodoraQueryKey,
    enabled: canConfigure,
    queryFn: () =>
      customInstance<ProviderAdmin>({
        url: '/api/admin/online-order-integrations/providers',
        method: 'GET',
        params: { provider: 'foodora' },
      }),
  });

  const save = useMutation({
    mutationFn: (values: ProviderForm) =>
      customInstance<{ id: string }>({
        url: '/api/admin/online-order-integrations/providers',
        method: 'PUT',
        data: {
          provider: 'wolt',
          enabled: values.enabled,
          clientId: values.clientId?.trim() || null,
          clientSecret: values.clientSecret?.trim() || null,
        },
      }),
    onSuccess: async () => {
      notify.successKey('onlineOrders.providers.saved');
      form.setFieldsValue({ clientId: '', clientSecret: '' });
      await queryClient.invalidateQueries({ queryKey: woltQueryKey });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'OnlineOrderProviders.save',
        fallbackKey: 'onlineOrders.providers.saveFailed',
      });
    },
  });

  const foodoraSave = useMutation({
    mutationFn: (enabled: boolean) =>
      customInstance<{ id: string }>({
        url: '/api/admin/online-order-integrations/providers',
        method: 'PUT',
        data: {
          provider: 'foodora',
          enabled,
        },
      }),
    onSuccess: async () => {
      notify.successKey('onlineOrders.providers.saved');
      await queryClient.invalidateQueries({ queryKey: foodoraQueryKey });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'OnlineOrderProviders.foodoraSave',
        fallbackKey: 'onlineOrders.providers.saveFailed',
      });
    },
  });

  if (!canConfigure) return null;

  const credentialStatus = status.data?.credentialStatus === 'configured'
    ? t('onlineOrders.providers.statusConfigured')
    : t('onlineOrders.providers.statusMissing');

  return (
    <section>
      <Typography.Title level={4}>{t('onlineOrders.providers.adminTitle')}</Typography.Title>
      <Typography.Paragraph type="secondary">
        {t('onlineOrders.providers.adminSubtitle')} {credentialStatus}
      </Typography.Paragraph>
      <Form<ProviderForm>
        form={form}
        layout="vertical"
        initialValues={{ enabled: status.data?.enabled ?? false }}
        onFinish={(values) => save.mutate(values)}
      >
        <Form.Item name="enabled" label={t('onlineOrders.providers.enabled')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="clientId" label={t('onlineOrders.providers.clientId')}>
          <Input autoComplete="off" />
        </Form.Item>
        <Form.Item name="clientSecret" label={t('onlineOrders.providers.clientSecret')}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>
          {t('onlineOrders.providers.save')}
        </Button>
      </Form>
      <FoodoraEnableForm
        enabled={foodora.data?.enabled === true}
        saving={foodoraSave.isPending}
        onSave={(enabled) => foodoraSave.mutate(enabled)}
      />
    </section>
  );
}

function FoodoraEnableForm({
  enabled,
  saving,
  onSave,
}: {
  enabled: boolean;
  saving: boolean;
  onSave: (enabled: boolean) => void;
}) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ enabled: boolean }>();

  return (
    <Form<{ enabled: boolean }>
      form={form}
      layout="vertical"
      initialValues={{ enabled }}
      key={enabled ? 'foodora-on' : 'foodora-off'}
      onFinish={(values) => onSave(values.enabled)}
    >
      <Typography.Title level={4}>{t('onlineOrders.providers.foodoraTitle')}</Typography.Title>
      <Form.Item name="enabled" label={t('onlineOrders.providers.foodoraEnabled')} valuePropName="checked">
        <Switch />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={saving}>
        {t('onlineOrders.providers.foodoraSave')}
      </Button>
    </Form>
  );
}
