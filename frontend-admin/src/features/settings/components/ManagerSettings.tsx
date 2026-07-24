'use client';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  Form,
  Input,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd';
import Link from 'next/link';
import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';

import { usePutApiAdminCashRegistersId } from '@/api/generated/admin/admin';
import { CardSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ChangeMyPasswordForm } from '@/features/settings/components/ChangeMyPasswordForm';
import { LanguageSelector } from '@/features/settings/components/LanguageSelector';
import { TenantTseStatusCard } from '@/features/settings/components/TenantTseStatusCard';
import { useTenantSettings } from '@/features/settings/hooks/useTenantSettings';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const EMPTY = '—';

export function ManagerSettings() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { canManageCashRegisters } = usePermissions();
  const {
    data: settings,
    isLoading,
    isError,
    error,
    refetch,
    isFetching,
    registerOptions,
    selectedRegisterId,
    setSelectedRegisterId,
    selectedRegister,
  } = useTenantSettings();

  const [registerForm] = Form.useForm<{ registerLocation: string }>();
  const updateRegisterMutation = usePutApiAdminCashRegistersId();
  const [savingRegister, setSavingRegister] = useState(false);

  useEffect(() => {
    registerForm.setFieldsValue({
      registerLocation: settings?.registerLocation ?? '',
    });
  }, [registerForm, settings?.registerLocation, selectedRegisterId]);

  const pageTitle = t('settings.manager.title');
  const breadcrumbs = [adminOverviewCrumb(t), { title: pageTitle }];

  const handleSaveRegister = async (values: { registerLocation: string }) => {
    if (!selectedRegisterId || !selectedRegister?.registerNumber?.trim()) {
      message.error(t('settings.manager.cashRegister.missing'));
      return;
    }

    setSavingRegister(true);
    try {
      await updateRegisterMutation.mutateAsync({
        id: selectedRegisterId,
        data: {
          location: values.registerLocation.trim(),
          registerNumber: selectedRegister.registerNumber.trim(),
        },
      });
      message.success(t('settings.manager.cashRegister.saveSuccess'));
      await refetch();
    } catch {
      message.error(t('settings.manager.cashRegister.saveFailed'));
    } finally {
      setSavingRegister(false);
    }
  };

  if (isLoading) {
    return (
      <PageShell>
        <Form form={registerForm} style={{ display: 'none' }} preserve />
        <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
        <CardSkeleton count={2} />
      </PageShell>
    );
  }

  if (isError) {
    return (
      <PageShell>
        <Form form={registerForm} style={{ display: 'none' }} preserve />
        <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
        <Alert
          type="error"
          title={t('settings.page.loadErrorTitle')}
          description={
            error instanceof Error && error.message.trim()
              ? error.message.trim()
              : t('settings.page.loadErrorFallback')
          }
          showIcon
          action={
            <Button size="small" type="primary" onClick={() => void refetch()} loading={isFetching}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </PageShell>
    );
  }

  const taxRateLabel = settings?.taxRate != null ? `${settings.taxRate}%` : EMPTY;

  return (
    <PageShell>
      <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <Card title={t('settings.manager.company.title')}>
          <Descriptions column={2} bordered size="small">
            <Descriptions.Item label={t('settings.manager.company.name')}>
              {settings?.companyName || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.company.vatId')}>
              {settings?.vatId || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.company.address')} span={2}>
              {settings?.address || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.company.taxRate')}>
              <Tag color="blue">{taxRateLabel}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.company.status')}>
              <Tag color={settings?.isActive ? 'green' : 'default'}>
                {settings?.isActive
                  ? t('settings.manager.statusActive')
                  : t('settings.manager.statusInactive')}
              </Tag>
            </Descriptions.Item>
          </Descriptions>

          <Alert
            type="info"
            title={t('settings.manager.readOnly.title')}
            description={t('settings.manager.readOnly.companyDescription')}
            showIcon
            style={{ marginTop: 12 }}
          />
        </Card>

        <Card title={t('settings.manager.appearance.title')}>
          <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
            {t('settings.personalization.language.description')}
          </Typography.Paragraph>
          <LanguageSelector />
        </Card>

        <Card title={t('settings.manager.cashRegister.title')}>
          {registerOptions.length > 1 ? (
            <div style={{ marginBottom: 16 }}>
              <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                {t('settings.manager.cashRegister.select')}
              </Typography.Text>
              <Select
                value={selectedRegisterId}
                onChange={(value) => setSelectedRegisterId(value)}
                options={registerOptions.map((option) => ({
                  value: option.value,
                  label: option.label,
                }))}
                style={{ maxWidth: 420, width: '100%' }}
              />
            </div>
          ) : null}

          <Form
            form={registerForm}
            layout="vertical"
            onFinish={handleSaveRegister}
            disabled={!canManageCashRegisters || !selectedRegisterId}
          >
            <Form.Item
              name="registerLocation"
              label={t('settings.manager.cashRegister.name')}
              rules={[{ required: true, message: t('settings.manager.cashRegister.required') }]}
              extra={
                settings?.registerNumber
                  ? t('settings.manager.cashRegister.numberHint', {
                      number: settings.registerNumber,
                    })
                  : undefined
              }
            >
              <Input placeholder={t('settings.manager.cashRegister.placeholder')} maxLength={100} />
            </Form.Item>

            <Form.Item>
              <Button
                type="primary"
                htmlType="submit"
                loading={savingRegister || updateRegisterMutation.isPending}
                disabled={!canManageCashRegisters}
              >
                {t('settings.manager.cashRegister.save')}
              </Button>
            </Form.Item>
          </Form>

          {!canManageCashRegisters ? (
            <Alert
              type="info"
              title={t('settings.manager.readOnly.title')}
              description={t('settings.manager.readOnly.registerDescription')}
              showIcon
            />
          ) : null}
        </Card>

        <TenantTseStatusCard />

        <Card title={t('settings.manager.fiscal.title')}>
          <Descriptions column={2} bordered size="small">
            <Descriptions.Item label={t('settings.manager.fiscal.tseStatus')}>
              <Tag color={settings?.tseConnected ? 'green' : 'red'}>
                {settings?.tseConnected
                  ? t('settings.manager.fiscal.connected')
                  : t('settings.manager.fiscal.disconnected')}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.fiscal.tseType')}>
              {settings?.tseType || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.fiscal.tseSerial')}>
              {settings?.tseSerial || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.fiscal.certificateValid')}>
              {settings?.certificateValidUntil || EMPTY}
            </Descriptions.Item>
            <Descriptions.Item label={t('settings.manager.fiscal.healthDetail')} span={2}>
              {settings?.tseStatusLabel || EMPTY}
            </Descriptions.Item>
          </Descriptions>

          <Space orientation="vertical" size="small" style={{ marginTop: 12, width: '100%' }}>
            <Alert
              type="info"
              title={t('settings.manager.readOnly.title')}
              description={t('settings.manager.readOnly.fiscalDescription')}
              showIcon
            />
            <Link href="/rksv/status">{t('settings.manager.fiscal.viewRksvStatus')}</Link>
          </Space>
        </Card>

        <ChangeMyPasswordForm />
      </Space>
    </PageShell>
  );
}

function PageShell({ children }: { children: ReactNode }) {
  return <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>{children}</div>;
}
