'use client';

import { SaveOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Select, Space, Switch, Typography } from 'antd';
import Link from 'next/link';
import { useEffect } from 'react';

import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { FormSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  fetchRksvRuntimeConfig,
  postRksvRuntimeConfig,
  rksvRuntimeConfigQueryKey,
  type RksvRuntimeConfigDto,
  type RksvRuntimeConfigPostDto,
} from '@/features/rksv-runtime-config/api';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTimeSeconds } from '@/lib/dateUtils';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type FormValues = {
  mode: string;
  tseMode: string;
  finanzOnlineMode: string;
  showDemoLabel: boolean;
  bypassTseInDevelopment: boolean;
  reason?: string;
};

function dtoToForm(d: RksvRuntimeConfigDto): FormValues {
  return {
    mode: d.mode,
    tseMode: d.tseMode,
    finanzOnlineMode: d.finanzOnlineMode,
    showDemoLabel: d.showDemoLabel,
    bypassTseInDevelopment: d.bypassTseInDevelopment,
    reason: '',
  };
}

export default function AdminRksvRuntimeConfigPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<FormValues>();

  const query = useQuery({
    queryKey: rksvRuntimeConfigQueryKey,
    queryFn: ({ signal }) => fetchRksvRuntimeConfig(signal),
    staleTime: 15_000,
  });

  useEffect(() => {
    if (query.data) {
      form.setFieldsValue(dtoToForm(query.data));
    }
  }, [form, query.data]);

  const saveMutation = useMutation({
    mutationFn: (body: RksvRuntimeConfigPostDto) => postRksvRuntimeConfig(body),
    onSuccess: async (data) => {
      form.setFieldsValue(dtoToForm(data));
      await queryClient.invalidateQueries({ queryKey: rksvRuntimeConfigQueryKey });
      await queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.environment });
      notify.success(t('rksvRuntimeConfig.page.saveSuccess'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'RksvRuntimeConfig.save',
        fallbackKey: 'rksvRuntimeConfig.page.saveFailed',
      });
    },
  });

  const handleSave = async () => {
    const values = await form.validateFields();
    modal.confirm({
      title: t('rksvRuntimeConfig.page.saveConfirmTitle'),
      content: t('rksvRuntimeConfig.page.saveConfirmContent'),
      okText: t('rksvRuntimeConfig.page.saveConfirmOk'),
      onOk: () =>
        saveMutation.mutateAsync({
          mode: values.mode,
          tseMode: values.tseMode,
          finanzOnlineMode: values.finanzOnlineMode,
          showDemoLabel: values.showDemoLabel,
          bypassTseInDevelopment: values.bypassTseInDevelopment,
          reason: values.reason?.trim() || undefined,
        }),
    });
  };

  const data = query.data;
  const tseModeWatch = Form.useWatch('tseMode', form);
  const tseModeIsReal = tseModeWatch === 'Real';
  const updatedLine = data?.updatedAtUtc
    ? data.updatedBy
      ? t('rksvRuntimeConfig.page.lastUpdated', {
          date: formatDateTimeSeconds(data.updatedAtUtc),
          user: data.updatedBy,
        })
      : t('rksvRuntimeConfig.page.lastUpdatedUnknownUser', {
          date: formatDateTimeSeconds(data.updatedAtUtc),
        })
    : null;

  return (
    <div style={{ padding: 24 }}>
      <AdminPageHeader
        title={t('rksvRuntimeConfig.page.title')}
        subtitle={t('rksvRuntimeConfig.page.subtitle')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', {
          title: t('rksvRuntimeConfig.page.title'),
        })}
      />

      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Alert type="error" showIcon title={t('rksvRuntimeConfig.page.fiscalWarning')} />
        <Alert type="warning" showIcon title={t('rksvRuntimeConfig.page.warning')} />
        <Alert
          type="info"
          showIcon
          title={t('rksvRuntimeConfig.page.devModeNote')}
          action={
            <Link href="/settings/development-mode">
              <Button size="small">{t('rksvRuntimeConfig.page.devModeLink')}</Button>
            </Link>
          }
        />

        {query.isError ? (
          <Alert
            type="error"
            showIcon
            title={t('rksvRuntimeConfig.page.loadError')}
            description={
              <ApiErrorAlertDescription
                t={t}
                error={query.error}
                logContext="RksvRuntimeConfig.load"
                fallbackKey="common.messages.unknownError"
              />
            }
          />
        ) : null}

        {query.isLoading && !data ? (
          <FormSkeleton />
        ) : (
          <Card>
            {data ? (
              <Space orientation="vertical" size="small" style={{ marginBottom: 16 }}>
                <Typography.Text type="secondary">
                  {t('rksvRuntimeConfig.page.hostLabel')}: {data.hostEnvironment}
                  {' · '}
                  {t('rksvRuntimeConfig.page.sourceLabel')}:{' '}
                  {data.source === 'database'
                    ? t('rksvRuntimeConfig.page.sourceDatabase')
                    : t('rksvRuntimeConfig.page.sourceAppsettings')}
                </Typography.Text>
                {data.appsettingsFallback ? (
                  <Typography.Text type="secondary">
                    {t('rksvRuntimeConfig.page.fallbackTitle')}: {data.appsettingsFallback.mode}
                    {' / '}
                    {data.appsettingsFallback.tseMode}
                    {' / '}
                    {data.appsettingsFallback.finanzOnlineMode}
                    {' / '}
                    {String(data.appsettingsFallback.showDemoLabel)}
                  </Typography.Text>
                ) : null}
                {updatedLine ? (
                  <Typography.Text type="secondary">{updatedLine}</Typography.Text>
                ) : null}
                {data.productionLockApplies ? (
                  <Alert
                    type={data.productionLockOk ? 'success' : 'error'}
                    showIcon
                    title={
                      data.productionLockOk
                        ? t('rksvRuntimeConfig.page.lockOk')
                        : t('rksvRuntimeConfig.page.lockBlocked')
                    }
                    description={
                      data.productionLockReasons.length > 0
                        ? data.productionLockReasons.join(' · ')
                        : undefined
                    }
                  />
                ) : null}
                {data.tseHealthBypassBlockedByRealTseMode ? (
                  <Alert type="success" showIcon title={t('rksvRuntimeConfig.page.tseBypassBlockedByReal')} />
                ) : null}
                {data.tseHealthBypassEffective ? (
                  <Alert type="warning" showIcon title={t('rksvRuntimeConfig.page.tseBypassEffective')} />
                ) : null}
              </Space>
            ) : null}

            <Form<FormValues>
              form={form}
              layout="vertical"
              disabled={saveMutation.isPending || query.isFetching}
            >
              <Form.Item
                label={t('rksvRuntimeConfig.page.mode')}
                name="mode"
                rules={[{ required: true }]}
              >
                <Select
                  options={[
                    { value: 'Demo', label: t('rksvRuntimeConfig.page.modeDemo') },
                    { value: 'Production', label: t('rksvRuntimeConfig.page.modeProduction') },
                  ]}
                />
              </Form.Item>
              <Form.Item
                label={t('rksvRuntimeConfig.page.tseMode')}
                name="tseMode"
                rules={[{ required: true }]}
              >
                <Select
                  options={[
                    { value: 'Simulation', label: t('rksvRuntimeConfig.page.tseSimulation') },
                    { value: 'Real', label: t('rksvRuntimeConfig.page.tseReal') },
                  ]}
                />
              </Form.Item>
              <Form.Item
                label={t('rksvRuntimeConfig.page.fonMode')}
                name="finanzOnlineMode"
                rules={[{ required: true }]}
              >
                <Select
                  options={[
                    { value: 'Simulation', label: t('rksvRuntimeConfig.page.fonSimulation') },
                    { value: 'Real', label: t('rksvRuntimeConfig.page.fonReal') },
                  ]}
                />
              </Form.Item>
              <Form.Item
                label={t('rksvRuntimeConfig.page.showDemoLabel')}
                name="showDemoLabel"
                valuePropName="checked"
                extra={t('rksvRuntimeConfig.page.showDemoLabelHint')}
              >
                <Switch />
              </Form.Item>
              <Form.Item
                label={t('rksvRuntimeConfig.page.bypassTseInDevelopment')}
                name="bypassTseInDevelopment"
                valuePropName="checked"
                extra={t('rksvRuntimeConfig.page.bypassTseInDevelopmentHint')}
              >
                <Switch disabled={tseModeIsReal} />
              </Form.Item>
              <Form.Item label={t('rksvRuntimeConfig.page.reason')} name="reason">
                <Input.TextArea rows={2} placeholder={t('rksvRuntimeConfig.page.reasonPlaceholder')} />
              </Form.Item>
              <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
                {t('rksvRuntimeConfig.page.hardwareNote')}
              </Typography.Paragraph>
              <Button
                type="primary"
                icon={<SaveOutlined />}
                loading={saveMutation.isPending}
                onClick={() => void handleSave()}
              >
                {t('rksvRuntimeConfig.page.save')}
              </Button>
            </Form>
          </Card>
        )}
      </Space>
    </div>
  );
}
