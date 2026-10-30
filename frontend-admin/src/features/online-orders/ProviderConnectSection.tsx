'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Typography } from 'antd';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type ProviderTenant = {
  provider: string;
  enabled: boolean;
  credentialStatus: 'configured' | 'missing' | string;
  connected: boolean;
};

export function ProviderConnectSection() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canConnect = hasPermission(PERMISSIONS.DIGITAL_ORDERS_MANAGE);

  const status = useQuery({
    queryKey: ['online-order-integration-provider-status', 'wolt'],
    enabled: canConnect,
    queryFn: () =>
      customInstance<ProviderTenant>({
        url: '/api/admin/online-order-integrations/providers/status',
        method: 'GET',
        params: { provider: 'wolt' },
      }),
  });

  const connect = useMutation({
    mutationFn: () =>
      customInstance<{ authorizationUrl: string }>({
        url: '/api/admin/online-order-integrations/wolt/connect',
        method: 'POST',
      }),
    onSuccess: (result) => {
      if (result.authorizationUrl) {
        window.location.assign(result.authorizationUrl);
      }
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'OnlineOrderProviders.connect',
        fallbackKey: 'onlineOrders.providers.connectFailed',
      });
    },
  });

  if (!canConnect) return null;

  const enabled = status.data?.enabled === true;
  const connection = status.data?.connected
    ? t('onlineOrders.providers.connected')
    : t('onlineOrders.providers.notConnected');

  return (
    <section>
      <Typography.Title level={4}>{t('onlineOrders.providers.tenantTitle')}</Typography.Title>
      <Typography.Paragraph>
        {enabled ? t('onlineOrders.providers.statusConfigured') : t('onlineOrders.providers.disabled')}{' '}
        {connection}
      </Typography.Paragraph>
      <Button
        type="primary"
        disabled={!enabled}
        loading={connect.isPending}
        onClick={() => connect.mutate()}
      >
        {t('onlineOrders.providers.connect')}
      </Button>
    </section>
  );
}
