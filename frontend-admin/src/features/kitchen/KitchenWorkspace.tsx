'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Form, InputNumber, Row, Space, Switch, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect } from 'react';

import {
  kitchenQueryKeys,
  updateKitchenSettings,
  useKitchenAnalytics,
  useKitchenOrders,
  useKitchenSettings,
  type KitchenAdminOrder,
  type KitchenApiScope,
  type KitchenSettings,
} from '@/api/admin/kitchen';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export function KitchenWorkspace({
  tenantId,
  requireKitchenDisplay = true,
}: {
  tenantId?: string;
  requireKitchenDisplay?: boolean;
}) {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const scope: KitchenApiScope = tenantId ? { tenantId } : {};
  const canWrite = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const ambientProfile = useAmbientVerticalProfile(!tenantId && requireKitchenDisplay);
  const kitchenDisplay = tenantId
    ? true
    : ambientProfile.data?.posFeatures?.kitchenDisplay === true;

  const settings = useKitchenSettings(scope, { enabled: kitchenDisplay || !requireKitchenDisplay });
  const orders = useKitchenOrders(scope, { enabled: kitchenDisplay || !requireKitchenDisplay });
  const analytics = useKitchenAnalytics(scope, { enabled: kitchenDisplay || !requireKitchenDisplay });

  const [form] = Form.useForm<KitchenSettings>();
  useEffect(() => {
    if (settings.data) {
      form.setFieldsValue(settings.data);
    }
  }, [form, settings.data]);

  const save = useMutation({
    mutationFn: (body: KitchenSettings) => updateKitchenSettings(body, scope),
    onSuccess: (data) => {
      queryClient.setQueryData(kitchenQueryKeys.settings(tenantId), data);
      notify.success(t('tenants.kitchen.saved'));
    },
    onError: () => notify.error(t('tenants.kitchen.saveFailed')),
  });

  if (requireKitchenDisplay && !tenantId && ambientProfile.isFetched && !kitchenDisplay) {
    return <Alert type="info" showIcon title={t('tenants.kitchen.unavailable')} />;
  }

  const columns: ColumnsType<KitchenAdminOrder> = [
    {
      title: t('tenants.kitchen.colTable'),
      dataIndex: 'tableNumber',
      render: (value: string | null | undefined) =>
        value?.trim() ? value : t('tenants.kitchen.takeAway'),
    },
    {
      title: t('tenants.kitchen.colStatus'),
      dataIndex: 'status',
      render: (value: string) => t(`tenants.kitchen.status.${value}`, { defaultValue: value }),
    },
    {
      title: t('tenants.kitchen.colNotes'),
      dataIndex: 'notes',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('tenants.kitchen.colItems'),
      dataIndex: 'items',
      render: (_: unknown, row) =>
        row.items.map((item) => `${item.quantity} × ${item.productName}`).join(', ') || '—',
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Card title={t('tenants.kitchen.settingsTitle')} loading={settings.isLoading}>
        <Form
          form={form}
          layout="vertical"
          disabled={!canWrite}
          onFinish={(values) => save.mutate(values)}
        >
          <Form.Item
            name="autoClearMinutes"
            label={t('tenants.kitchen.autoClear')}
            extra={t('tenants.kitchen.autoClearHelp')}
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={240} aria-label={t('tenants.kitchen.autoClear')} />
          </Form.Item>
          <Form.Item
            name="soundEnabled"
            label={t('tenants.kitchen.sound')}
            extra={t('tenants.kitchen.soundHelp')}
            valuePropName="checked"
          >
            <Switch aria-label={t('tenants.kitchen.sound')} />
          </Form.Item>
          {canWrite ? (
            <Button type="primary" htmlType="submit" loading={save.isPending}>
              {t('tenants.kitchen.save')}
            </Button>
          ) : (
            <Typography.Text type="secondary">{t('tenants.kitchen.readOnly')}</Typography.Text>
          )}
        </Form>
      </Card>

      <Card title={t('tenants.kitchen.analyticsTitle')} loading={analytics.isLoading}>
        <Row gutter={16}>
          <Col xs={24} md={8}>
            <Typography.Text type="secondary">{t('tenants.kitchen.avgPrep')}</Typography.Text>
            <Typography.Title level={3} style={{ marginTop: 4 }}>
              {analytics.data?.averagePrepMinutes == null
                ? '—'
                : t('tenants.kitchen.avgPrepValue', {
                    minutes: analytics.data.averagePrepMinutes,
                  })}
            </Typography.Title>
          </Col>
          <Col xs={24} md={8}>
            <Typography.Text type="secondary">{t('tenants.kitchen.ordersPerHour')}</Typography.Text>
            <Typography.Title level={3} style={{ marginTop: 4 }}>
              {analytics.data?.ordersPerHour ?? '—'}
            </Typography.Title>
          </Col>
          <Col xs={24} md={8}>
            <Typography.Text type="secondary">{t('tenants.kitchen.lastHour')}</Typography.Text>
            <Typography.Title level={3} style={{ marginTop: 4 }}>
              {analytics.data?.createdLastHour ?? '—'}
            </Typography.Title>
          </Col>
        </Row>
      </Card>

      <Card title={t('tenants.kitchen.liveTitle')} loading={orders.isLoading}>
        <Table
          rowKey="id"
          dataSource={orders.data ?? []}
          columns={columns}
          pagination={false}
          locale={{ emptyText: t('tenants.kitchen.empty') }}
        />
      </Card>
    </Space>
  );
}
