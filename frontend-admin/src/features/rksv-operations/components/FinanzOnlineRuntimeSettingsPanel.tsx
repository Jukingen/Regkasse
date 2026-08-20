'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Col, Form, Row, Segmented, Select, Space, Switch, Tag, Typography } from 'antd';
import { useEffect } from 'react';

import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  getFinanzOnlineRuntimeSettings,
  updateFinanzOnlineRuntimeSettings,
  type FinanzOnlineOutboxWorkerRangeDto,
  type FinanzOnlineRuntimeSettingsDto,
  type UpdateFinanzOnlineRuntimeRequest,
} from '@/features/rksv-operations/api/finanzOnlineOutboxWorker';
import {
  finanzOnlineTransportProfileTagColor,
  resolveFinanzOnlineTransportProfile,
  runtimeRequestForTransportProfile,
  type FinanzOnlineTransportProfile,
} from '@/features/rksv-operations/utils/finanzOnlineTransportProfile';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type RuntimeFormValues = {
  retryIntervalSeconds: number;
  retryMaxRetryCount: number;
  retryBaseDelaySeconds: number;
  retryBatchSize: number;
};

function selectOptions(range: FinanzOnlineOutboxWorkerRangeDto | undefined, extras: number[]) {
  const values = new Set<number>([...(range?.values ?? []), ...extras.filter((n) => Number.isFinite(n))]);
  return [...values]
    .sort((a, b) => a - b)
    .map((value) => ({ value, label: String(value) }));
}

function profileLabelKey(profile: FinanzOnlineTransportProfile) {
  switch (profile) {
    case 'demo':
      return 'finanzOnlineOutbox.runtime.profileDemo' as const;
    case 'bmfTest':
      return 'finanzOnlineOutbox.runtime.profileBmfTest' as const;
    case 'production':
      return 'finanzOnlineOutbox.runtime.profileProduction' as const;
    default:
      return 'finanzOnlineOutbox.runtime.profileIncomplete' as const;
  }
}

function currentProfile(data: FinanzOnlineRuntimeSettingsDto | undefined): FinanzOnlineTransportProfile | null {
  if (!data) return null;
  return resolveFinanzOnlineTransportProfile({
    isProduction: data.isProduction,
    useSimulation: data.useSimulation,
    enableRealTestSubmission: data.enableRealTestSubmission,
    enableRealTestQuery: data.enableRealTestQuery,
  });
}

export function FinanzOnlineRuntimeSettingsPanel() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<RuntimeFormValues>();
  const settingsKey = rksvAdminQueryKeys.finanzOnlineOutbox.runtimeSettings();
  const readinessKey = rksvAdminQueryKeys.finanzOnlineOutbox.readiness();

  const query = useQuery({
    queryKey: settingsKey,
    queryFn: ({ signal }) => getFinanzOnlineRuntimeSettings(signal),
    staleTime: 15_000,
  });
  const data = query.data;
  const profile = currentProfile(data);

  useEffect(() => {
    if (!data) return;
    form.setFieldsValue({
      retryIntervalSeconds: data.retryIntervalSeconds.effective,
      retryMaxRetryCount: data.retryMaxRetryCount.effective,
      retryBaseDelaySeconds: data.retryBaseDelaySeconds.effective,
      retryBatchSize: data.retryBatchSize.effective,
    });
  }, [data, form]);

  const mutation = useMutation({
    mutationFn: (body: UpdateFinanzOnlineRuntimeRequest) =>
      updateFinanzOnlineRuntimeSettings(body),
    onSuccess: async (next) => {
      queryClient.setQueryData(settingsKey, next);
      await queryClient.invalidateQueries({ queryKey: readinessKey });
      notify.successKey('finanzOnlineOutbox.runtime.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FinanzOnlineRuntime.settings',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const canManage = data?.canManage === true;
  const hasOverride = data?.source === 'global_override';

  const applyProfile = (next: Extract<FinanzOnlineTransportProfile, 'demo' | 'bmfTest'>) => {
    if (profile === next) return;
    mutation.mutate(runtimeRequestForTransportProfile(next));
  };

  const onProfileChange = (value: string | number) => {
    if (value !== 'demo' && value !== 'bmfTest') return;
    if (value === 'bmfTest') {
      modal.confirm({
        title: t('finanzOnlineOutbox.runtime.confirmBmfTestTitle'),
        content: t('finanzOnlineOutbox.runtime.confirmBmfTestContent'),
        okText: t('common.buttons.confirm'),
        cancelText: t('common.buttons.cancel'),
        onOk: () => mutation.mutateAsync(runtimeRequestForTransportProfile('bmfTest')),
      });
      return;
    }
    applyProfile('demo');
  };

  const selectorOptions = [
    {
      label: t('finanzOnlineOutbox.runtime.profileDemo'),
      value: 'demo',
    },
    {
      label: t('finanzOnlineOutbox.runtime.profileBmfTest'),
      value: 'bmfTest',
    },
    ...(profile === 'incomplete'
      ? [
          {
            label: t('finanzOnlineOutbox.runtime.profileIncomplete'),
            value: 'incomplete',
            disabled: true,
          },
        ]
      : []),
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Typography.Text strong>{t('finanzOnlineOutbox.runtime.title')}</Typography.Text>
      {profile ? (
        <Space wrap size="small" align="center">
          <Tag color={data?.isProduction ? 'red' : 'default'}>
            {data?.isProduction
              ? t('finanzOnlineOutbox.runtime.hostProduction')
              : t('finanzOnlineOutbox.runtime.hostDevelopment')}
          </Tag>
          <Tag color={finanzOnlineTransportProfileTagColor(profile)}>
            {t('finanzOnlineOutbox.runtime.statusFon')}: {t(profileLabelKey(profile))}
          </Tag>
          <Tag>
            {hasOverride
              ? t('finanzOnlineOutbox.runtime.sourceOverrideShort')
              : t('finanzOnlineOutbox.runtime.sourceConfigShort')}
          </Tag>
        </Space>
      ) : null}
      <Typography.Text type="secondary">
        {data?.isProduction
          ? t('finanzOnlineOutbox.runtime.hintProd')
          : t('finanzOnlineOutbox.runtime.hintDev')}
      </Typography.Text>

      {data?.isProduction ? (
        <Typography.Text type="secondary">
          {t('finanzOnlineOutbox.runtime.productionLockedHint')}
        </Typography.Text>
      ) : (
        <Space direction="vertical" size={4} style={{ width: '100%' }}>
          <Typography.Text>{t('finanzOnlineOutbox.runtime.selectorLabel')}</Typography.Text>
          <Segmented
            value={profile ?? undefined}
            options={selectorOptions}
            disabled={!canManage || query.isError || query.isLoading || mutation.isPending}
            onChange={onProfileChange}
          />
          {profile === 'incomplete' ? (
            <Typography.Text type="warning">
              {t('finanzOnlineOutbox.runtime.incompleteHint')}
            </Typography.Text>
          ) : null}
        </Space>
      )}

      <Space wrap size="middle" align="center">
        <Switch
          checked={data?.retryJobEnabled ?? false}
          loading={mutation.isPending || query.isLoading}
          disabled={!canManage || query.isError}
          onChange={(checked) => mutation.mutate({ retryJobEnabled: checked })}
        />
        <Typography.Text>{t('finanzOnlineOutbox.runtime.retryJobEnabled')}</Typography.Text>
        {canManage && hasOverride ? (
          <Button size="small" disabled={mutation.isPending} onClick={() => mutation.mutate({ clearOverride: true })}>
            {t('finanzOnlineOutbox.runtime.resetToConfig')}
          </Button>
        ) : null}
      </Space>

      <Form
        form={form}
        layout="vertical"
        disabled={!canManage || query.isLoading || query.isError}
        onFinish={(values) => {
          mutation.mutate(values);
        }}
      >
        <Row gutter={[12, 0]}>
          <Col xs={24} sm={12} md={8}>
            <Form.Item
              name="retryIntervalSeconds"
              label={t('finanzOnlineOutbox.runtime.retryInterval')}
              extra={t('finanzOnlineOutbox.runtime.configValue', {
                value: String(data?.retryIntervalSeconds.config ?? '—'),
              })}
            >
              <Select
                options={selectOptions(data?.allowed.retryIntervalSeconds, [
                  data?.retryIntervalSeconds.config,
                  data?.retryIntervalSeconds.effective,
                ].filter((n): n is number => typeof n === 'number'))}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={8}>
            <Form.Item name="retryMaxRetryCount" label={t('finanzOnlineOutbox.runtime.retryMax')}>
              <Select
                options={selectOptions(data?.allowed.retryMaxRetryCount, [
                  data?.retryMaxRetryCount.config,
                  data?.retryMaxRetryCount.effective,
                ].filter((n): n is number => typeof n === 'number'))}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={8}>
            <Form.Item name="retryBatchSize" label={t('finanzOnlineOutbox.runtime.retryBatch')}>
              <Select
                options={selectOptions(data?.allowed.retryBatchSize, [
                  data?.retryBatchSize.config,
                  data?.retryBatchSize.effective,
                ].filter((n): n is number => typeof n === 'number'))}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
        </Row>
        {canManage ? (
          <Button type="primary" htmlType="submit" loading={mutation.isPending}>
            {t('finanzOnlineOutbox.runtime.saveParameters')}
          </Button>
        ) : null}
      </Form>

      {!canManage && !query.isLoading ? (
        <Typography.Text type="secondary">
          {t('finanzOnlineOutbox.runtime.readOnlyHint')}
        </Typography.Text>
      ) : null}
    </Space>
  );
}
