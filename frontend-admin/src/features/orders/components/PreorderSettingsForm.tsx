'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, InputNumber, Space } from 'antd';
import { useEffect } from 'react';

import {
  adminPreorderQueryKeys,
  fetchAdminPreorderSettings,
  updateAdminPreorderSettings,
  type AdminPreorderSettings,
} from '@/features/orders/api/preordersApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

export function PreorderSettingsForm() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<AdminPreorderSettings>();

  const query = useQuery({
    queryKey: adminPreorderQueryKeys.settings,
    queryFn: fetchAdminPreorderSettings,
  });

  useEffect(() => {
    if (query.data) {
      form.setFieldsValue(query.data);
    }
  }, [form, query.data]);

  const mutation = useMutation({
    mutationFn: updateAdminPreorderSettings,
    onSuccess: async (data) => {
      form.setFieldsValue(data);
      await queryClient.invalidateQueries({ queryKey: adminPreorderQueryKeys.settings });
      notify.successKey('onlineOrders.preorder.settingsSaved');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'PreorderSettings.save',
        fallbackKey: 'onlineOrders.preorder.settingsSaveFailed',
      });
    },
  });

  return (
    <Card title={t('onlineOrders.preorder.settingsTitle')}>
      <p style={{ color: '#64748b', marginTop: 0 }}>{t('onlineOrders.preorder.settingsHint')}</p>
      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => mutation.mutate(values)}
        disabled={query.isLoading || mutation.isPending}>
        <Form.Item
          name="pickupDeadlineWeeks"
          label={t('onlineOrders.preorder.settingsWeeks')}
          rules={[{ required: true }]}>
          <InputNumber min={1} max={52} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item
          name="cancellationPolicyText"
          label={t('onlineOrders.preorder.settingsPolicy')}
          rules={[{ required: true, max: 500 }]}>
          <Input.TextArea
            rows={2}
            maxLength={500}
            placeholder={t('onlineOrders.preorder.settingsPolicyDefault')}
          />
        </Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" loading={mutation.isPending}>
            {t('onlineOrders.preorder.settingsSave')}
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
