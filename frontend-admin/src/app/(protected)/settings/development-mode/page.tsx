'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useEffect, useMemo } from 'react';
import { Alert, Button, Card, Form, InputNumber, Select, Space, Spin, Switch, Typography } from 'antd';
import { SaveOutlined, UndoOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import {
  developmentModeSettingsQueryKey,
  fetchDevelopmentModeSettings,
  putDevelopmentModeSettings,
  type DevelopmentModeSettingsDto,
  type DevelopmentModeSettingsPutDto,
} from '@/features/development-mode/developmentModeApi';
import {
  DEVELOPMENT_MODE_FEATURE_IDS,
  type DevelopmentModeFeatureId,
} from '@/features/development-mode/licenseFeatureOptions';

type DevModeFormValues = {
  enabled: boolean;
  bypassLicense: boolean;
  bypassNtpCheck: boolean;
  bypassTseCheck: boolean;
  simulateOffline: boolean;
  forceOnline: boolean;
  validDays: number;
  features: string[];
};

const FEATURE_LABEL_KEY: Record<DevelopmentModeFeatureId, string> = {
  pos_fiscal: 'developmentMode.features.pos_fiscal',
  pos_offline: 'developmentMode.features.pos_offline',
  admin_basic: 'developmentMode.features.admin_basic',
  admin_rksv: 'developmentMode.features.admin_rksv',
  admin_license_manage: 'developmentMode.features.admin_license_manage',
};

function dtoToForm(d: DevelopmentModeSettingsDto): DevModeFormValues {
  return {
    enabled: d.enabled,
    bypassLicense: d.bypassLicense,
    bypassNtpCheck: d.bypassNtpCheck,
    bypassTseCheck: d.bypassTseCheck,
    simulateOffline: d.simulateOffline,
    forceOnline: d.forceOnline,
    validDays: d.validDays,
    features: Array.isArray(d.features) ? [...d.features] : [],
  };
}

function formToPut(v: DevModeFormValues): DevelopmentModeSettingsPutDto {
  return {
    enabled: v.enabled,
    bypassLicense: v.bypassLicense,
    bypassNtpCheck: v.bypassNtpCheck,
    bypassTseCheck: v.bypassTseCheck,
    simulateOffline: v.simulateOffline,
    forceOnline: v.forceOnline,
    validDays: v.validDays,
    features: v.features ?? [],
  };
}

export default function DevelopmentModeSettingsPage() {
  const { t } = useI18n();
  const { data, isLoading, isError, error, refetch, isFetching } = useQuery({
    queryKey: developmentModeSettingsQueryKey,
    queryFn: fetchDevelopmentModeSettings,
    staleTime: 30_000,
  });

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
    { title: t(ADMIN_NAV_LABEL_KEYS.developmentMode), href: '/settings/development-mode' },
  ];

  if (isLoading && !data) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <AdminPageHeader title={t('developmentMode.page.title')} breadcrumbs={breadcrumbs} />
        <Card>
          <div style={{ textAlign: 'center', padding: 48 }}>
            <Spin size="large" />
          </div>
        </Card>
      </div>
    );
  }

  if (isError) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <AdminPageHeader title={t('developmentMode.page.title')} breadcrumbs={breadcrumbs} />
        <Alert
          type="error"
          showIcon
          title={t('developmentMode.page.loadError')}
          description={error instanceof Error ? error.message : undefined}
        />
        <Button onClick={() => refetch()}>{t('common.buttons.retry')}</Button>
      </div>
    );
  }

  if (!data) {
    return null;
  }

  return (
    <DevelopmentModeSettingsForm
      data={data}
      breadcrumbs={breadcrumbs}
      isFetching={isFetching}
    />
  );
}

function DevelopmentModeSettingsForm({
  data,
  breadcrumbs,
  isFetching,
}: {
  data: DevelopmentModeSettingsDto;
  breadcrumbs: { title: string; href?: string }[];
  isFetching: boolean;
}) {
  const { message } = useAntdApp();
  const { t, formatLocale } = useI18n();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<DevModeFormValues>();
  const enabled = Form.useWatch('enabled', form);

  useEffect(() => {
    form.setFieldsValue(dtoToForm(data));
  }, [data, form]);

  const featureOptions = useMemo(
    () =>
      DEVELOPMENT_MODE_FEATURE_IDS.map((id) => ({
        value: id,
        label: t(FEATURE_LABEL_KEY[id]),
      })),
    [t],
  );

  const saveMutation = useMutation({
    mutationFn: putDevelopmentModeSettings,
    onSuccess: (next) => {
      queryClient.setQueryData(developmentModeSettingsQueryKey, next);
      form.setFieldsValue(dtoToForm(next));
      message.success(t('developmentMode.page.saveSuccess'));
    },
    onError: () => {
      message.error(t('developmentMode.page.saveFailed'));
    },
  });

  const formatUpdatedLine = (d: DevelopmentModeSettingsDto) => {
    const date = new Date(d.updatedAtUtc).toLocaleString(formatLocale, {
      dateStyle: 'medium',
      timeStyle: 'short',
    });
    const user = d.updatedBy?.trim();
    if (user) return t('developmentMode.page.lastUpdated', { date, user });
    return t('developmentMode.page.lastUpdatedUnknownUser', { date });
  };

  const handleSave = async () => {
    const values = await form.validateFields();
    await saveMutation.mutateAsync(formToPut(values));
  };

  const handleReset = async () => {
    const fresh = await queryClient.fetchQuery({
      queryKey: developmentModeSettingsQueryKey,
      queryFn: fetchDevelopmentModeSettings,
    });
    form.setFieldsValue(dtoToForm(fresh));
    message.info(t('developmentMode.page.resetHint'));
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader title={t('developmentMode.page.title')} breadcrumbs={breadcrumbs} />

      <Alert
        type="warning"
        showIcon
        banner
        title={t('developmentMode.page.warning')}
        style={{
          border: '1px solid #d48806',
          background: 'linear-gradient(90deg, #fff7e6 0%, #fffbe6 100%)',
        }}
      />

      <Card>
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          <Typography.Text type="secondary" style={{ display: 'block' }}>
            {formatUpdatedLine(data)}
          </Typography.Text>

          <Form<DevModeFormValues>
            form={form}
            layout="vertical"
            initialValues={{
              enabled: false,
              bypassLicense: false,
              bypassNtpCheck: false,
              bypassTseCheck: false,
              simulateOffline: false,
              forceOnline: false,
              validDays: 365,
              features: [],
            }}
            disabled={saveMutation.isPending || isFetching}
          >
            <Form.Item label={t('developmentMode.page.toggleMain')} name="enabled" valuePropName="checked">
              <Switch />
            </Form.Item>

            {enabled ? (
              <Space
                orientation="vertical"
                size="middle"
                style={{ width: '100%', paddingLeft: 8, borderLeft: '3px solid #faad14' }}
              >
                <Form.Item label={t('developmentMode.page.toggleBypassLicense')} name="bypassLicense" valuePropName="checked">
                  <Switch />
                </Form.Item>
                <Form.Item label={t('developmentMode.page.toggleBypassNtp')} name="bypassNtpCheck" valuePropName="checked">
                  <Switch />
                </Form.Item>
                <Form.Item label={t('developmentMode.page.toggleBypassTse')} name="bypassTseCheck" valuePropName="checked">
                  <Switch />
                </Form.Item>
                <Form.Item label={t('developmentMode.page.toggleSimulateOffline')} name="simulateOffline" valuePropName="checked">
                  <Switch />
                </Form.Item>
                <Form.Item label={t('developmentMode.page.toggleForceOnline')} name="forceOnline" valuePropName="checked">
                  <Switch />
                </Form.Item>
              </Space>
            ) : null}

            <Form.Item
              label={t('developmentMode.page.validDays')}
              name="validDays"
              rules={[
                { required: true },
                {
                  type: 'number',
                  min: 1,
                  max: 3650,
                },
              ]}
            >
              <InputNumber min={1} max={3650} step={1} style={{ width: '100%', maxWidth: 280 }} />
            </Form.Item>

            <Form.Item label={t('developmentMode.page.features')} name="features">
              <Select
                mode="multiple"
                allowClear
                optionFilterProp="label"
                placeholder={t('developmentMode.page.features')}
                options={featureOptions}
                style={{ width: '100%' }}
              />
            </Form.Item>

            <Space wrap>
              <Button type="primary" icon={<SaveOutlined />} loading={saveMutation.isPending} onClick={() => void handleSave()}>
                {t('developmentMode.page.save')}
              </Button>
              <Button icon={<UndoOutlined />} onClick={() => void handleReset()} disabled={saveMutation.isPending}>
                {t('developmentMode.page.reset')}
              </Button>
            </Space>
          </Form>
        </Space>
      </Card>
    </div>
  );
}
