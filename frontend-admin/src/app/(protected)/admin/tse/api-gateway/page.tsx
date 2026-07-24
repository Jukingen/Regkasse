'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Card,
  Col,
  Form,
  InputNumber,
  Progress,
  Row,
  Select,
  Space,
  Statistic,
  Switch,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  configureTseGateway,
  getTseGatewayConfig,
  getTseGatewayStatus,
  routeTseGatewayRequest,
} from '@/features/tse-api-gateway/api/gateway';
import type { TseGatewayEndpoint } from '@/features/tse-api-gateway/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-api-gateway'] as const;

type ConfigForm = {
  enabled: boolean;
  strategy: string;
  healthCheckInterval: number;
  timeout: number;
  retryCount: number;
};

export default function TseApiGatewayPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [form] = Form.useForm<ConfigForm>();

  const statusQuery = useQuery({
    queryKey: [...KEY, 'status'],
    queryFn: ({ signal }) => getTseGatewayStatus(signal),
    enabled: allowed,
    refetchInterval: 15_000,
  });

  const configQuery = useQuery({
    queryKey: [...KEY, 'config'],
    queryFn: ({ signal }) => getTseGatewayConfig(signal),
    enabled: allowed,
  });

  useEffect(() => {
    const config = configQuery.data;
    if (!config) return;
    form.setFieldsValue({
      enabled: config.enabled,
      strategy: config.strategy,
      healthCheckInterval: config.healthCheckInterval,
      timeout: config.timeout,
      retryCount: config.retryCount,
    });
  }, [configQuery.data, form]);

  const saveMutation = useMutation({
    mutationFn: (values: ConfigForm) => {
      const source = configQuery.data?.endpoints ?? statusQuery.data?.endpoints ?? [];
      const endpoints =
        source.length > 0
          ? source.map((ep, index) => ({
              id: ep.id,
              provider: ep.provider,
              endpoint: ep.endpoint,
              weight: ep.weight || 1,
              enabled: ep.enabled,
              sortOrder: ep.sortOrder ?? index,
            }))
          : [
              {
                provider: 'fake',
                endpoint: 'local://fake-tse',
                weight: 1,
                enabled: true,
                sortOrder: 0,
              },
            ];
      return configureTseGateway({
        strategy: values.strategy,
        healthCheckInterval: values.healthCheckInterval,
        timeout: values.timeout,
        retryCount: values.retryCount,
        enabled: values.enabled,
        endpoints,
      });
    },
    onSuccess: async () => {
      notify.success(t('tseApiGateway.configSaved'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseApiGateway.saveConfig',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const probeMutation = useMutation({
    mutationFn: () => routeTseGatewayRequest({ operation: 'HealthProbe' }),
    onSuccess: async (result) => {
      if (result.success) {
        notify.success(t('tseApiGateway.probeSuccess'), {
          mode: 'notification',
          description: result.message,
        });
      } else {
        notify.warning(t('tseApiGateway.probeFailed'), {
          mode: 'notification',
          description: result.message,
        });
      }
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseApiGateway.probe',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const stats = statusQuery.data?.stats;
  const endpoints = statusQuery.data?.endpoints ?? [];

  const columns: ColumnsType<TseGatewayEndpoint> = [
    { title: t('tseApiGateway.colProvider'), dataIndex: 'provider', key: 'provider' },
    { title: t('tseApiGateway.colEndpoint'), dataIndex: 'endpoint', key: 'endpoint', ellipsis: true },
    {
      title: t('tseApiGateway.colStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (s: string) => (
        <Badge
          status={s === 'healthy' ? 'success' : s === 'unhealthy' ? 'error' : 'default'}
          text={
            s === 'healthy'
              ? t('tseApiGateway.statusHealthy')
              : s === 'unhealthy'
                ? t('tseApiGateway.statusUnhealthy')
                : t('tseApiGateway.statusUnknown')
          }
        />
      ),
    },
    {
      title: t('tseApiGateway.colLoad'),
      dataIndex: 'load',
      key: 'load',
      width: 140,
      render: (l: number) => <Progress percent={l ?? 0} size="small" />,
    },
    { title: t('tseApiGateway.colRequests'), dataIndex: 'requests', key: 'requests' },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseApiGateway.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseApiGateway.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseApiGateway.title') }]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseApiGateway.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {statusQuery.isError ? (
        <Alert type="error" showIcon message={t('tseApiGateway.loadError')} />
      ) : (
        <Card
          title={t('tseApiGateway.cardTitle')}
          loading={statusQuery.isLoading || configQuery.isLoading}
        >
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseApiGateway.scopeNote')}
          />

          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}>
              <Statistic
                title={t('tseApiGateway.totalRequests')}
                value={stats?.totalRequests ?? 0}
              />
            </Col>
            <Col xs={24} md={8}>
              <Statistic
                title={t('tseApiGateway.successRate')}
                value={stats?.successRate ?? 100}
                suffix="%"
              />
            </Col>
            <Col xs={24} md={8}>
              <Statistic
                title={t('tseApiGateway.avgResponse')}
                value={stats?.avgResponseTime ?? 0}
                suffix="ms"
              />
            </Col>
          </Row>

          <Form
            form={form}
            layout="vertical"
            onFinish={(values) => saveMutation.mutate(values)}
            initialValues={{
              enabled: true,
              strategy: 'RoundRobin',
              healthCheckInterval: 30,
              timeout: 5000,
              retryCount: 3,
            }}
          >
            <Row gutter={16}>
              <Col xs={24} md={6}>
                <Form.Item name="enabled" valuePropName="checked" label={t('tseApiGateway.enabled')}>
                  <Switch />
                </Form.Item>
              </Col>
              <Col xs={24} md={6}>
                <Form.Item name="strategy" label={t('tseApiGateway.strategy')}>
                  <Select
                    options={[
                      { value: 'RoundRobin', label: t('tseApiGateway.strategyRoundRobin') },
                      {
                        value: 'LeastConnections',
                        label: t('tseApiGateway.strategyLeastConnections'),
                      },
                      { value: 'Weighted', label: t('tseApiGateway.strategyWeighted') },
                    ]}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} md={4}>
                <Form.Item name="healthCheckInterval" label={t('tseApiGateway.healthInterval')}>
                  <InputNumber min={5} max={3600} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={4}>
                <Form.Item name="timeout" label={t('tseApiGateway.timeout')}>
                  <InputNumber min={500} max={60000} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={4}>
                <Form.Item name="retryCount" label={t('tseApiGateway.retryCount')}>
                  <InputNumber min={0} max={10} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>

            <Space wrap style={{ marginBottom: 16 }}>
              <Button type="default" htmlType="submit" loading={saveMutation.isPending}>
                {t('tseApiGateway.saveConfig')}
              </Button>
              <Button
                type="primary"
                loading={probeMutation.isPending}
                onClick={() => probeMutation.mutate()}
              >
                {t('tseApiGateway.probeNow')}
              </Button>
            </Space>
          </Form>

          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={endpoints}
            columns={columns}
          />
        </Card>
      )}
    </>
  );
}
