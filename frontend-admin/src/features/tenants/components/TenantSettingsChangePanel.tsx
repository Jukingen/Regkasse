'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Form,
  Input,
  Row,
  Select,
  Space,
  Typography,
} from 'antd';
import { useEffect, useState } from 'react';

import { ImpactSimulator } from '@/components/ImpactSimulator';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { simulateImpact } from '@/features/impact/api/impactSimulation';
import type { ImpactReport } from '@/features/impact/types';
import {
  type CurrentTenantSettings,
  type FiscalSettingsValue,
  type TenantSettingType,
  type TenantSettingsHistoryItem,
  approveSettingsChange,
  getSettingsHistory,
  getTenantSettings,
  rejectSettingsChange,
  requestSettingsChange,
  revertSettingsChange,
  tenantSettingsQueryKeys,
} from '@/features/tenants/api/tenantSettings';
import { TenantSettingsAudit } from '@/features/tenants/components/TenantSettingsAudit';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type RequestFormValues = {
  settingType: TenantSettingType;
  currency?: string;
  country?: string;
  timeZone?: string;
  reason: string;
  companyName?: string;
  companyAddress?: string;
  companyTaxNumber?: string;
  companyVatNumber?: string;
  companyRegistrationNumber?: string;
};

const CURRENCY_OPTIONS = [
  { value: 'EUR', labelKey: 'tenants.settingsChange.currencies.EUR' },
] as const;

const COUNTRY_OPTIONS = [
  { value: 'AT', labelKey: 'tenants.settingsChange.countries.AT' },
  { value: 'DE', labelKey: 'tenants.settingsChange.countries.DE' },
] as const;

const TIMEZONE_OPTIONS = [
  'Europe/Vienna',
  'Europe/Berlin',
  'Europe/Zurich',
  'Europe/Istanbul',
  'UTC',
] as const;

function buildNewValue(
  values: RequestFormValues
): string | FiscalSettingsValue {
  switch (values.settingType) {
    case 'currency':
      return values.currency ?? '';
    case 'country':
      return values.country ?? '';
    case 'timezone':
      return values.timeZone ?? '';
    case 'fiscal_settings':
      return {
        companyName: values.companyName?.trim() ?? '',
        companyAddress: values.companyAddress?.trim() ?? '',
        companyTaxNumber: values.companyTaxNumber?.trim() ?? '',
        companyVatNumber: values.companyVatNumber?.trim() || null,
        companyRegistrationNumber: values.companyRegistrationNumber?.trim() || null,
      };
    default:
      return '';
  }
}

export type TenantSettingsChangePanelProps = {
  tenantId: string;
};

export function TenantSettingsChangePanel({ tenantId }: TenantSettingsChangePanelProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<RequestFormValues>();
  const settingType = Form.useWatch('settingType', form);
  const [historyVisible, setHistoryVisible] = useState(true);
  const [impactOpen, setImpactOpen] = useState(false);
  const [impactReport, setImpactReport] = useState<ImpactReport | null>(null);
  const [pendingRequest, setPendingRequest] = useState<RequestFormValues | null>(null);
  const [impactLoading, setImpactLoading] = useState(false);

  const settingsQuery = useQuery({
    queryKey: tenantSettingsQueryKeys.current(tenantId),
    queryFn: () => getTenantSettings(tenantId),
    enabled: Boolean(tenantId),
  });

  const historyQuery = useQuery({
    queryKey: tenantSettingsQueryKeys.history(tenantId),
    queryFn: () => getSettingsHistory(tenantId),
    enabled: Boolean(tenantId) && historyVisible,
  });

  useEffect(() => {
    const settings = settingsQuery.data;
    if (!settings) return;
    form.setFieldsValue({
      settingType: 'currency',
      currency: settings.currency,
      country: settings.country,
      timeZone: settings.timeZone,
      companyName: settings.fiscalSettings.companyName,
      companyAddress: settings.fiscalSettings.companyAddress,
      companyTaxNumber: settings.fiscalSettings.companyTaxNumber,
      companyVatNumber: settings.fiscalSettings.companyVatNumber ?? undefined,
      companyRegistrationNumber:
        settings.fiscalSettings.companyRegistrationNumber ?? undefined,
      reason: undefined,
    });
  }, [settingsQuery.data, form]);

  const invalidateAll = () => {
    void queryClient.invalidateQueries({ queryKey: tenantSettingsQueryKeys.current(tenantId) });
    void queryClient.invalidateQueries({ queryKey: tenantSettingsQueryKeys.history(tenantId) });
  };

  const requestMutation = useMutation({
    mutationFn: (values: RequestFormValues) =>
      requestSettingsChange({
        tenantId,
        settingType: values.settingType,
        newValue: buildNewValue(values),
        reason: values.reason.trim(),
      }),
    onSuccess: (result) => {
      if (result.warning) {
        notify.warning(result.warning);
      }
      notify.successKey('tenants.settingsChange.messages.requestSuccess');
      form.setFieldValue('reason', undefined);
      invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TenantSettings.request',
        fallbackKey: 'tenants.settingsChange.messages.requestFailed',
      });
    },
  });

  const approveMutation = useMutation({
    mutationFn: (changeId: string) => approveSettingsChange(changeId),
    onSuccess: () => {
      notify.successKey('tenants.settingsChange.messages.approveSuccess');
      invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TenantSettings.approve',
        fallbackKey: 'tenants.settingsChange.messages.approveFailed',
      });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: ({ changeId, reason }: { changeId: string; reason: string }) =>
      rejectSettingsChange(changeId, reason),
    onSuccess: () => {
      notify.successKey('tenants.settingsChange.messages.rejectSuccess');
      invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TenantSettings.reject',
        fallbackKey: 'tenants.settingsChange.messages.rejectFailed',
      });
    },
  });

  const revertMutation = useMutation({
    mutationFn: ({ changeId, reason }: { changeId: string; reason: string }) =>
      revertSettingsChange(changeId, reason),
    onSuccess: () => {
      notify.successKey('tenants.settingsChange.messages.revertSuccess');
      invalidateAll();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TenantSettings.revert',
        fallbackKey: 'tenants.settingsChange.messages.revertFailed',
      });
    },
  });

  const confirmRequest = async (values: RequestFormValues) => {
    // Currency changes go through the impact simulator first.
    if (values.settingType === 'currency' && values.currency) {
      setImpactLoading(true);
      try {
        const report = await simulateImpact({
          tenantId,
          changeType: 'Currency',
          newCurrency: values.currency,
        });
        setPendingRequest(values);
        setImpactReport(report);
        setImpactOpen(true);
        return;
      } catch (err) {
        notify.apiError(err, {
          logContext: 'TenantSettings.impactSimulate',
          fallbackKey: 'impactSimulator.loadError',
        });
        return;
      } finally {
        setImpactLoading(false);
      }
    }

    const preview = buildNewValue(values);
    const previewText =
      typeof preview === 'string' ? preview : JSON.stringify(preview, null, 2);

    modal.confirm({
      title: t('tenants.settingsChange.confirmRequest.title'),
      content: (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            <Typography.Text strong>
              {t('tenants.settingsChange.confirmRequest.type')}:{' '}
            </Typography.Text>
            {t(`tenants.settingsChange.settingTypes.${values.settingType}`)}
          </Typography.Paragraph>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            <Typography.Text strong>
              {t('tenants.settingsChange.confirmRequest.newValue')}:{' '}
            </Typography.Text>
            <Typography.Text code>{previewText}</Typography.Text>
          </Typography.Paragraph>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            <Typography.Text strong>
              {t('tenants.settingsChange.confirmRequest.reason')}:{' '}
            </Typography.Text>
            {values.reason}
          </Typography.Paragraph>
          <Alert
            type="warning"
            showIcon
            title={t('tenants.settingsChange.confirmRequest.warningTitle')}
            description={t('tenants.settingsChange.confirmRequest.warningBody')}
          />
        </Space>
      ),
      okText: t('tenants.settingsChange.confirmRequest.ok'),
      cancelText: t('common.buttons.cancel'),
      onOk: () => requestMutation.mutateAsync(values),
    });
  };

  const closeImpactSimulator = () => {
    if (requestMutation.isPending) return;
    setImpactOpen(false);
    setImpactReport(null);
    setPendingRequest(null);
  };

  const confirmApprove = (record: TenantSettingsHistoryItem) => {
    modal.confirm({
      title: t('tenants.settingsChange.confirmApprove.title'),
      content: t('tenants.settingsChange.confirmApprove.body'),
      okText: t('tenants.settingsChange.actions.approve'),
      cancelText: t('common.buttons.cancel'),
      onOk: () => approveMutation.mutateAsync(record.id),
    });
  };

  const promptReject = (record: TenantSettingsHistoryItem) => {
    let reason = '';
    modal.confirm({
      title: t('tenants.settingsChange.confirmReject.title'),
      content: (
        <Input.TextArea
          rows={3}
          placeholder={t('tenants.settingsChange.confirmReject.reasonPlaceholder')}
          onChange={(e) => {
            reason = e.target.value;
          }}
        />
      ),
      okText: t('tenants.settingsChange.actions.reject'),
      okButtonProps: { danger: true },
      cancelText: t('common.buttons.cancel'),
      onOk: () => {
        if (!reason.trim()) {
          notify.errorKey('tenants.settingsChange.validation.reasonRequired');
          return Promise.reject(new Error('reason required'));
        }
        return rejectMutation.mutateAsync({ changeId: record.id, reason: reason.trim() });
      },
    });
  };

  const promptRevert = (record: TenantSettingsHistoryItem) => {
    let reason = '';
    modal.confirm({
      title: t('tenants.settingsChange.confirmRevert.title'),
      content: (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            {t('tenants.settingsChange.confirmRevert.body')}
          </Typography.Paragraph>
          <Input.TextArea
            rows={3}
            placeholder={t('tenants.settingsChange.confirmRevert.reasonPlaceholder')}
            onChange={(e) => {
              reason = e.target.value;
            }}
          />
        </Space>
      ),
      okText: t('tenants.settingsChange.actions.revert'),
      cancelText: t('common.buttons.cancel'),
      onOk: () => {
        if (!reason.trim()) {
          notify.errorKey('tenants.settingsChange.validation.reasonRequired');
          return Promise.reject(new Error('reason required'));
        }
        return revertMutation.mutateAsync({ changeId: record.id, reason: reason.trim() });
      },
    });
  };

  const settings: CurrentTenantSettings | undefined = settingsQuery.data;
  const historyBusy =
    approveMutation.isPending ||
    rejectMutation.isPending ||
    revertMutation.isPending;
  const hasFiscalData = Boolean(settings?.hasFiscalData);
  const countryLocked = hasFiscalData;
  const currencyOptions = [
    ...CURRENCY_OPTIONS.map((o) => ({
      value: o.value,
      label: t(o.labelKey),
    })),
    ...(settings?.currency &&
    !CURRENCY_OPTIONS.some((o) => o.value === settings.currency)
      ? [{ value: settings.currency, label: `${settings.currency} (legacy)` }]
      : []),
  ];
  const countryOptions = [
    ...COUNTRY_OPTIONS.map((o) => ({
      value: o.value,
      label: t(o.labelKey),
      disabled: countryLocked && o.value !== settings?.country,
    })),
    ...(settings?.country &&
    !COUNTRY_OPTIONS.some((o) => o.value === settings.country)
      ? [
          {
            value: settings.country,
            label: `${settings.country} (legacy)`,
            disabled: countryLocked,
          },
        ]
      : []),
  ];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        title={t('tenants.settingsChange.fourEyesTitle')}
        description={t('tenants.settingsChange.fourEyesBody')}
      />

      {hasFiscalData ? (
        <Alert
          type="warning"
          showIcon
          title={t('tenants.settingsChange.fiscalWarning.title')}
          description={t('tenants.settingsChange.fiscalWarning.body')}
        />
      ) : null}

      {settings?.hasInvoices ? (
        <Alert
          type="info"
          showIcon
          title={t('tenants.settingsChange.invoiceWarning.title')}
          description={t('tenants.settingsChange.invoiceWarning.body')}
        />
      ) : null}

      <Card
        title={t('tenants.settingsChange.currentTitle')}
        loading={settingsQuery.isLoading}
      >
        {settingsQuery.isError ? (
          <Alert
            type="error"
            showIcon
            title={t('tenants.settingsChange.messages.loadFailed')}
          />
        ) : settings ? (
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} md={6}>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.fields.currency')}
              </Typography.Text>
              <div>
                <Typography.Text strong>{settings.currency}</Typography.Text>
              </div>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.fields.country')}
              </Typography.Text>
              <div>
                <Typography.Text strong>{settings.country}</Typography.Text>
              </div>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.fields.timeZone')}
              </Typography.Text>
              <div>
                <Typography.Text strong>{settings.timeZone}</Typography.Text>
              </div>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Typography.Text type="secondary">
                {t('tenants.settingsChange.fields.taxNumber')}
              </Typography.Text>
              <div>
                <Typography.Text strong>
                  {settings.fiscalSettings.companyTaxNumber}
                </Typography.Text>
              </div>
            </Col>
          </Row>
        ) : null}
      </Card>

      <Card title={t('tenants.settingsChange.requestTitle')}>
        <Form
          form={form}
          layout="vertical"
          onFinish={confirmRequest}
          initialValues={{ settingType: 'currency' }}
        >
          <Form.Item
            name="settingType"
            label={t('tenants.settingsChange.fields.settingType')}
            rules={[
              {
                required: true,
                message: t('tenants.settingsChange.validation.settingTypeRequired'),
              },
            ]}
          >
            <Select
              options={[
                {
                  value: 'currency',
                  label: t('tenants.settingsChange.settingTypes.currency'),
                },
                {
                  value: 'country',
                  label: t('tenants.settingsChange.settingTypes.country'),
                },
                {
                  value: 'timezone',
                  label: t('tenants.settingsChange.settingTypes.timezone'),
                },
                {
                  value: 'fiscal_settings',
                  label: t('tenants.settingsChange.settingTypes.fiscal_settings'),
                },
              ]}
            />
          </Form.Item>

          {settingType === 'currency' ? (
            <Form.Item
              name="currency"
              label={t('tenants.settingsChange.fields.currency')}
              rules={[
                {
                  required: true,
                  message: t('tenants.settingsChange.validation.currencyRequired'),
                },
              ]}
            >
              <Select options={currencyOptions} />
            </Form.Item>
          ) : null}

          {settingType === 'country' ? (
            <Form.Item
              name="country"
              label={t('tenants.settingsChange.fields.country')}
              extra={
                countryLocked
                  ? t('tenants.settingsChange.fiscalWarning.countryLockedHint')
                  : undefined
              }
              rules={[
                {
                  required: true,
                  message: t('tenants.settingsChange.validation.countryRequired'),
                },
              ]}
            >
              <Select options={countryOptions} disabled={countryLocked} />
            </Form.Item>
          ) : null}

          {settingType === 'timezone' ? (
            <Form.Item
              name="timeZone"
              label={t('tenants.settingsChange.fields.timeZone')}
              rules={[
                {
                  required: true,
                  message: t('tenants.settingsChange.validation.timeZoneRequired'),
                },
              ]}
            >
              <Select
                options={TIMEZONE_OPTIONS.map((tz) => ({ value: tz, label: tz }))}
                showSearch
              />
            </Form.Item>
          ) : null}

          {settingType === 'fiscal_settings' ? (
            <Row gutter={16}>
              <Col xs={24} md={12}>
                <Form.Item
                  name="companyName"
                  label={t('tenants.settingsChange.fields.companyName')}
                  rules={[
                    {
                      required: true,
                      message: t('tenants.settingsChange.validation.companyNameRequired'),
                    },
                  ]}
                >
                  <Input maxLength={100} />
                </Form.Item>
              </Col>
              <Col xs={24} md={12}>
                <Form.Item
                  name="companyTaxNumber"
                  label={t('tenants.settingsChange.fields.taxNumber')}
                  rules={[
                    {
                      required: true,
                      message: t('tenants.settingsChange.validation.taxNumberRequired'),
                    },
                    {
                      pattern: /^ATU\d{8}$/i,
                      message: t('tenants.settingsChange.validation.taxNumberFormat'),
                    },
                  ]}
                >
                  <Input maxLength={20} placeholder="ATU12345678" />
                </Form.Item>
              </Col>
              <Col span={24}>
                <Form.Item
                  name="companyAddress"
                  label={t('tenants.settingsChange.fields.companyAddress')}
                  rules={[
                    {
                      required: true,
                      message: t(
                        'tenants.settingsChange.validation.companyAddressRequired'
                      ),
                    },
                  ]}
                >
                  <Input.TextArea rows={2} maxLength={200} />
                </Form.Item>
              </Col>
              <Col xs={24} md={12}>
                <Form.Item
                  name="companyVatNumber"
                  label={t('tenants.settingsChange.fields.vatNumber')}
                >
                  <Input maxLength={20} />
                </Form.Item>
              </Col>
              <Col xs={24} md={12}>
                <Form.Item
                  name="companyRegistrationNumber"
                  label={t('tenants.settingsChange.fields.registrationNumber')}
                >
                  <Input maxLength={20} />
                </Form.Item>
              </Col>
            </Row>
          ) : null}

          <Form.Item
            name="reason"
            label={t('tenants.settingsChange.fields.reason')}
            rules={[
              {
                required: true,
                message: t('tenants.settingsChange.validation.reasonRequired'),
              },
            ]}
          >
            <Input.TextArea
              rows={2}
              maxLength={1000}
              placeholder={t('tenants.settingsChange.fields.reasonPlaceholder')}
            />
          </Form.Item>

          <Space wrap>
            <Button
              type="primary"
              htmlType="submit"
              loading={requestMutation.isPending || impactLoading}
            >
              {t('tenants.settingsChange.actions.request')}
            </Button>
            <Button onClick={() => setHistoryVisible((v) => !v)}>
              {historyVisible
                ? t('tenants.settingsChange.actions.hideHistory')
                : t('tenants.settingsChange.actions.showHistory')}
            </Button>
          </Space>
        </Form>
      </Card>

      {historyVisible ? (
        <TenantSettingsAudit
          history={historyQuery.data ?? []}
          loading={historyQuery.isLoading}
          currentUserId={user?.id}
          busy={historyBusy}
          onApprove={confirmApprove}
          onReject={promptReject}
          onRevert={promptRevert}
        />
      ) : null}

      <ImpactSimulator
        open={impactOpen}
        impactReport={impactReport}
        confirmLoading={requestMutation.isPending}
        onClose={closeImpactSimulator}
        onConfirm={() => {
          if (!pendingRequest) return;
          void requestMutation.mutateAsync(pendingRequest).then(() => {
            closeImpactSimulator();
          });
        }}
      />
    </Space>
  );
}
