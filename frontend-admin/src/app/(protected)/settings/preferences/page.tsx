'use client';

import { Button, Card, Col, Form, Row, Select, Space, Switch, Typography } from 'antd';
import { useEffect } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useUserPreferences } from '@/hooks/useUserPreferences';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import type { UserTimeZone } from '@/lib/personalization/types';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

type PreferencesFormValues = {
  dateFormat: 'DD.MM.YYYY' | 'MM/DD/YYYY' | 'YYYY-MM-DD';
  timeZone: UserTimeZone;
  use24HourFormat: boolean;
  language: 'de' | 'en' | 'tr';
};

export default function UserPreferencesPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { preferences, updatePreferences, isUpdating } = useUserPreferences();
  const [form] = Form.useForm<PreferencesFormValues>();

  useEffect(() => {
    form.setFieldsValue({
      dateFormat: preferences.dateFormat,
      timeZone: preferences.timeZone,
      use24HourFormat: preferences.use24HourFormat,
      language: preferences.language,
    });
  }, [preferences, form]);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.preferences.pageTitle') },
  ];

  const onFinish = (values: PreferencesFormValues) => {
    updatePreferences({
      dateFormat: values.dateFormat,
      timeZone: values.timeZone,
      use24HourFormat: values.use24HourFormat,
      language: values.language,
    });
    notify.successKey('settings.preferences.saveSuccess');
  };

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('settings.preferences.pageTitle')} breadcrumbs={breadcrumbs} />
      <Typography.Paragraph type="secondary">
        {t('settings.preferences.intro')}
      </Typography.Paragraph>

      <Card title={t('settings.preferences.cardTitle')}>
        <Form
          form={form}
          layout="vertical"
          onFinish={onFinish}
          initialValues={{
            dateFormat: preferences.dateFormat,
            timeZone: preferences.timeZone,
            use24HourFormat: preferences.use24HourFormat,
            language: preferences.language,
          }}
        >
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item
                name="dateFormat"
                label={t('settings.preferences.fields.dateFormat')}
                rules={[{ required: true }]}
              >
                <Select
                  options={[
                    {
                      value: 'DD.MM.YYYY',
                      label: t('settings.personalization.appearance.dateFormatOptions.DD_MM_YYYY'),
                    },
                    {
                      value: 'MM/DD/YYYY',
                      label: t('settings.personalization.appearance.dateFormatOptions.MM_DD_YYYY'),
                    },
                    {
                      value: 'YYYY-MM-DD',
                      label: t('settings.personalization.appearance.dateFormatOptions.YYYY_MM_DD'),
                    },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="timeZone"
                label={t('settings.preferences.fields.timeZone')}
                rules={[{ required: true }]}
              >
                <Select
                  showSearch
                  options={[
                    { value: 'Europe/Vienna', label: 'Europe/Vienna' },
                    { value: 'Europe/Berlin', label: 'Europe/Berlin' },
                    { value: 'Europe/Zurich', label: 'Europe/Zurich' },
                    { value: 'Europe/London', label: 'Europe/London' },
                    { value: 'Europe/Istanbul', label: 'Europe/Istanbul' },
                    { value: 'America/New_York', label: 'America/New_York' },
                    { value: 'UTC', label: 'UTC' },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="language"
                label={t('settings.preferences.fields.language')}
                rules={[{ required: true }]}
              >
                <Select
                    options={[
                    { value: 'de', label: t('settings.language.localeDe') },
                    { value: 'en', label: t('settings.language.localeEn') },
                    { value: 'tr', label: t('settings.language.localeTr') },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="use24HourFormat"
                label={t('settings.preferences.fields.use24HourFormat')}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
            </Col>
          </Row>

          <Space>
            <Button type="primary" htmlType="submit" loading={isUpdating}>
              {t('settings.preferences.save')}
            </Button>
          </Space>
        </Form>
      </Card>
    </AdminPageShell>
  );
}
