'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Col, Form, Row, Select, Space, Switch, Typography } from 'antd';
import { useEffect } from 'react';

import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  getFinanzOnlineOutboxWorkerSettings,
  updateFinanzOnlineOutboxWorkerSettings,
  type FinanzOnlineOutboxWorkerRangeDto,
  type UpdateFinanzOnlineOutboxWorkerRequest,
} from '@/features/rksv-operations/api/finanzOnlineOutboxWorker';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type WorkerFormValues = {
  pollIntervalSeconds: number;
  maxAttempts: number;
  baseDelaySeconds: number;
  backoffCapSeconds: number;
  jitterMaxSeconds: number;
  processingTimeoutSeconds: number;
};

function selectOptions(range: FinanzOnlineOutboxWorkerRangeDto | undefined, extras: number[]) {
  const values = new Set<number>([...(range?.values ?? []), ...extras.filter((n) => Number.isFinite(n))]);
  return [...values]
    .sort((a, b) => a - b)
    .map((value) => ({ value, label: String(value) }));
}

export function FinanzOnlineOutboxWorkerSwitch() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<WorkerFormValues>();
  const settingsKey = rksvAdminQueryKeys.finanzOnlineOutbox.workerSettings();
  const readinessKey = rksvAdminQueryKeys.finanzOnlineOutbox.readiness();

  const query = useQuery({
    queryKey: settingsKey,
    queryFn: ({ signal }) => getFinanzOnlineOutboxWorkerSettings(signal),
    staleTime: 15_000,
  });

  const data = query.data;

  useEffect(() => {
    if (!data) return;
    form.setFieldsValue({
      pollIntervalSeconds: data.pollIntervalSeconds.effective,
      maxAttempts: data.maxAttempts.effective,
      baseDelaySeconds: data.baseDelaySeconds.effective,
      backoffCapSeconds: data.backoffCapSeconds.effective,
      jitterMaxSeconds: data.jitterMaxSeconds.effective,
      processingTimeoutSeconds: data.processingTimeoutSeconds.effective,
    });
  }, [data, form]);

  const mutation = useMutation({
    mutationFn: (body: UpdateFinanzOnlineOutboxWorkerRequest) =>
      updateFinanzOnlineOutboxWorkerSettings(body),
    onSuccess: async (next) => {
      queryClient.setQueryData(settingsKey, next);
      await queryClient.invalidateQueries({ queryKey: readinessKey });
      notify.successKey('finanzOnlineOutbox.workerSwitch.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FinanzOnlineOutbox.workerSettings',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const canManage = data?.canManage === true;
  const source = data?.source ?? 'config';
  const hasOverride = source === 'global_override';

  const applyEnabled = (enabled: boolean) => {
    if (data?.isProduction && !enabled) {
      modal.confirm({
        title: t('finanzOnlineOutbox.workerSwitch.prodDisableTitle'),
        content: t('finanzOnlineOutbox.workerSwitch.prodDisableContent'),
        okText: t('common.buttons.confirm'),
        cancelText: t('common.buttons.cancel'),
        onOk: () =>
          mutation.mutateAsync({
            enabled: false,
            confirmProductionDisable: true,
          }),
      });
      return;
    }

    mutation.mutate({ enabled });
  };

  const saveParameters = async (values: WorkerFormValues) => {
    await mutation.mutateAsync({
      pollIntervalSeconds: values.pollIntervalSeconds,
      maxAttempts: values.maxAttempts,
      baseDelaySeconds: values.baseDelaySeconds,
      backoffCapSeconds: values.backoffCapSeconds,
      jitterMaxSeconds: values.jitterMaxSeconds,
      processingTimeoutSeconds: values.processingTimeoutSeconds,
    });
  };

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap size="middle" align="center">
        <Switch
          checked={data?.enabled ?? false}
          loading={mutation.isPending || query.isLoading}
          disabled={!canManage || query.isError}
          onChange={(checked) => applyEnabled(checked)}
        />
        <Typography.Text>
          {t('finanzOnlineOutbox.workerSwitch.label')}
        </Typography.Text>
        {canManage && hasOverride ? (
          <Button
            size="small"
            disabled={mutation.isPending}
            onClick={() => mutation.mutate({ clearOverride: true })}
          >
            {t('finanzOnlineOutbox.workerSwitch.resetToConfig')}
          </Button>
        ) : null}
      </Space>
      <Typography.Text type="secondary">
        {data?.isProduction
          ? t('finanzOnlineOutbox.workerSwitch.hintProd')
          : t('finanzOnlineOutbox.workerSwitch.hintDev')}{' '}
        {source === 'global_override'
          ? t('finanzOnlineOutbox.workerSwitch.sourceOverride')
          : t('finanzOnlineOutbox.workerSwitch.sourceConfig')}{' '}
        {t('finanzOnlineOutbox.workerSwitch.configValue', {
          value: data?.configEnabled
            ? t('finanzOnlineOutbox.readiness.yes')
            : t('finanzOnlineOutbox.readiness.no'),
        })}
      </Typography.Text>

      <Form
        form={form}
        layout="vertical"
        disabled={!canManage || query.isLoading || query.isError}
        onFinish={(values) => {
          void saveParameters(values);
        }}
      >
        <Row gutter={[12, 0]}>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="pollIntervalSeconds"
              label={t('finanzOnlineOutbox.workerSwitch.pollInterval')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.pollIntervalSeconds.config ?? '—'),
              })}
              range={data?.allowed.pollIntervalSeconds}
              extras={[data?.pollIntervalSeconds.config, data?.pollIntervalSeconds.effective]}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="maxAttempts"
              label={t('finanzOnlineOutbox.workerSwitch.maxAttempts')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.maxAttempts.config ?? '—'),
              })}
              range={data?.allowed.maxAttempts}
              extras={[data?.maxAttempts.config, data?.maxAttempts.effective]}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="processingTimeoutSeconds"
              label={t('finanzOnlineOutbox.workerSwitch.processingTimeout')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.processingTimeoutSeconds.config ?? '—'),
              })}
              range={data?.allowed.processingTimeoutSeconds}
              extras={[
                data?.processingTimeoutSeconds.config,
                data?.processingTimeoutSeconds.effective,
              ]}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="baseDelaySeconds"
              label={t('finanzOnlineOutbox.workerSwitch.baseDelay')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.baseDelaySeconds.config ?? '—'),
              })}
              range={data?.allowed.baseDelaySeconds}
              extras={[data?.baseDelaySeconds.config, data?.baseDelaySeconds.effective]}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="backoffCapSeconds"
              label={t('finanzOnlineOutbox.workerSwitch.backoffCap')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.backoffCapSeconds.config ?? '—'),
              })}
              range={data?.allowed.backoffCapSeconds}
              extras={[data?.backoffCapSeconds.config, data?.backoffCapSeconds.effective]}
            />
          </Col>
          <Col xs={24} sm={12} md={8}>
            <ParameterSelect
              name="jitterMaxSeconds"
              label={t('finanzOnlineOutbox.workerSwitch.jitterMax')}
              extra={t('finanzOnlineOutbox.workerSwitch.configValue', {
                value: String(data?.jitterMaxSeconds.config ?? '—'),
              })}
              range={data?.allowed.jitterMaxSeconds}
              extras={[data?.jitterMaxSeconds.config, data?.jitterMaxSeconds.effective]}
            />
          </Col>
        </Row>
        {canManage ? (
          <Button type="primary" htmlType="submit" loading={mutation.isPending}>
            {t('finanzOnlineOutbox.workerSwitch.saveParameters')}
          </Button>
        ) : null}
      </Form>

      {!canManage && !query.isLoading ? (
        <Typography.Text type="secondary">
          {t('finanzOnlineOutbox.workerSwitch.readOnlyHint')}
        </Typography.Text>
      ) : null}
    </Space>
  );
}

function ParameterSelect({
  name,
  label,
  extra,
  range,
  extras,
}: {
  name: keyof WorkerFormValues;
  label: string;
  extra: string;
  range: FinanzOnlineOutboxWorkerRangeDto | undefined;
  extras: Array<number | undefined>;
}) {
  return (
    <Form.Item
      name={name}
      label={label}
      extra={extra}
      rules={[{ required: true }]}
    >
      <Select
        options={selectOptions(
          range,
          extras.filter((n): n is number => typeof n === 'number')
        )}
        style={{ width: '100%' }}
      />
    </Form.Item>
  );
}

