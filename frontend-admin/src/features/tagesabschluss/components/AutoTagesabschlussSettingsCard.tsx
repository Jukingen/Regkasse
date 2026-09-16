'use client';

import { Button, Card, Form, InputNumber, Select, Space, Switch, Typography } from 'antd';
import { useEffect } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

import {
  getAutoTagesabschlussSettings,
  putAutoTagesabschlussSettings,
  type AutoTagesabschlussOpenOrdersPolicy,
  type AutoTagesabschlussSettings,
} from '../api/autoCloseSettings';

const { Paragraph } = Typography;

export function AutoTagesabschlussSettingsCard() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission(PERMISSIONS.DAILY_CLOSING_EXECUTE);
  const [form] = Form.useForm<AutoTagesabschlussSettings>();

  const query = useQuery({
    queryKey: ['/api/Tagesabschluss/auto-close-settings'],
    queryFn: getAutoTagesabschlussSettings,
  });

  useEffect(() => {
    if (query.data) form.setFieldsValue(query.data);
  }, [form, query.data]);

  const mutation = useMutation({
    mutationFn: putAutoTagesabschlussSettings,
    onSuccess: (saved) => {
      form.setFieldsValue(saved);
      notify.successKey('tagesabschluss.autoClose.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'Tagesabschluss.autoCloseSettings',
        fallbackKey: 'tagesabschluss.autoClose.saveError',
      });
    },
  });

  return (
    <Card title={t('tagesabschluss.autoClose.title')} loading={query.isLoading}>
      <Paragraph type="secondary">{t('tagesabschluss.autoClose.intro')}</Paragraph>
      <Form
        form={form}
        layout="vertical"
        disabled={!canEdit || mutation.isPending}
        initialValues={{
          enabled: true,
          hourVienna: 3,
          minuteVienna: 0,
          openOrdersPolicy: 'ForceWithWarning',
          promptCashCount: true,
        }}
        onFinish={(values) => mutation.mutate(values)}
      >
        <Form.Item name="enabled" label={t('tagesabschluss.autoClose.enabled')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Space wrap>
          <Form.Item name="hourVienna" label={t('tagesabschluss.autoClose.hour')} rules={[{ required: true }]}>
            <InputNumber min={0} max={23} />
          </Form.Item>
          <Form.Item name="minuteVienna" label={t('tagesabschluss.autoClose.minute')} rules={[{ required: true }]}>
            <InputNumber min={0} max={59} />
          </Form.Item>
        </Space>
        <Form.Item
          name="openOrdersPolicy"
          label={t('tagesabschluss.autoClose.openOrdersPolicy')}
          rules={[{ required: true }]}
        >
          <Select<AutoTagesabschlussOpenOrdersPolicy>
            style={{ maxWidth: 420 }}
            options={[
              { value: 'ForceWithWarning', label: t('tagesabschluss.autoClose.openOrdersForce') },
              { value: 'NotifyAndContinue', label: t('tagesabschluss.autoClose.openOrdersNotify') },
              { value: 'Block', label: t('tagesabschluss.autoClose.openOrdersBlock') },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="promptCashCount"
          label={t('tagesabschluss.autoClose.promptCashCount')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
        {canEdit ? (
          <Button type="primary" htmlType="submit" loading={mutation.isPending}>
            {t('tagesabschluss.autoClose.save')}
          </Button>
        ) : null}
      </Form>
    </Card>
  );
}
