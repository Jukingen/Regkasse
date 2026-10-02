'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space, Switch, Typography } from 'antd';
import { useEffect } from 'react';

import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
  fetchMonatsbelegPolicy,
  monatsbelegPolicyQueryKey,
  putMonatsbelegPolicy,
  type MonatsbelegBlockingMode,
} from '@/features/settings/api/monatsbelegPolicy';

type FormValues = {
  blockingMode: MonatsbelegBlockingMode;
  autoMonatsbelegEnabled: boolean;
};

export function MonatsbelegPolicyInlineEditor() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<FormValues>();

  const query = useQuery({
    queryKey: monatsbelegPolicyQueryKey,
    queryFn: ({ signal }) => fetchMonatsbelegPolicy(signal),
  });

  const mutation = useMutation({
    mutationFn: putMonatsbelegPolicy,
    onSuccess: async (dto) => {
      form.setFieldsValue({
        blockingMode: dto.blockingMode,
        autoMonatsbelegEnabled: dto.autoMonatsbelegEnabled,
      });
      await queryClient.invalidateQueries({ queryKey: monatsbelegPolicyQueryKey });
      notify.successKey('settings.rksv.monatsbelegPolicy.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'MonatsbelegPolicyInlineEditor.save',
        fallbackKey: 'settings.rksv.monatsbelegPolicy.saveFailed',
      });
    },
  });

  useEffect(() => {
    if (!query.data) return;
    form.setFieldsValue({
      blockingMode: query.data.blockingMode,
      autoMonatsbelegEnabled: query.data.autoMonatsbelegEnabled,
    });
  }, [form, query.data]);

  if (query.isError) {
    return (
      <Card size="small" title={t('rksvHub.monatsbelegePage.policyInlineTitle')} style={{ marginBottom: 16 }}>
        <Alert
          type="error"
          showIcon
          title={t('settings.page.loadErrorTitle')}
          description={t('settings.rksv.monatsbelegPolicy.loadFailed')}
          action={
            <Button size="small" type="primary" onClick={() => void query.refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </Card>
    );
  }

  return (
    <Card
      size="small"
      title={t('rksvHub.monatsbelegePage.policyInlineTitle')}
      loading={query.isLoading}
      style={{ marginBottom: 16 }}
      data-testid="monatsbeleg-policy-editor"
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('rksvHub.monatsbelegePage.policyInlineIntro')}
      </Typography.Paragraph>
      <Form
        form={form}
        layout="inline"
        disabled={!canEdit || mutation.isPending}
        onFinish={(values) => {
          mutation.mutate({
            blockingMode: values.blockingMode,
            autoMonatsbelegEnabled: values.autoMonatsbelegEnabled,
          });
        }}
      >
        <Form.Item
          name="blockingMode"
          label={t('settings.rksv.monatsbelegPolicy.modeLabel')}
          rules={[{ required: true }]}
        >
          <Select
            style={{ minWidth: 280 }}
            options={[
              {
                value: 'Strict',
                label: t('settings.rksv.monatsbelegPolicy.modeStrict'),
              },
              {
                value: 'GracePeriod',
                label: t('settings.rksv.monatsbelegPolicy.modeGracePeriod'),
              },
              {
                value: 'WarningOnly',
                label: t('settings.rksv.monatsbelegPolicy.modeWarningOnly'),
              },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="autoMonatsbelegEnabled"
          label={t('settings.rksv.monatsbelegPolicy.autoLabel')}
          valuePropName="checked"
        >
          <Switch data-testid="monatsbeleg-policy-auto-switch" />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              type="primary"
              htmlType="submit"
              loading={mutation.isPending}
              disabled={!canEdit}
              data-testid="monatsbeleg-policy-save"
            >
              {t('common.buttons.save')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
      {!canEdit ? (
        <Alert
          type="info"
          showIcon
          style={{ marginTop: 12 }}
          title={t('settings.manager.readOnly.title')}
          description={t('rksvHub.monatsbelegePage.policyReadOnly')}
        />
      ) : null}
    </Card>
  );
}
