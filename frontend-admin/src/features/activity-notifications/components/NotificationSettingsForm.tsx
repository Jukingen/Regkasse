'use client';

import { Alert, Button, Checkbox, Divider, Form, Input, Select, Space, Switch } from 'antd';
import { useEffect } from 'react';

import type { NotificationConfig } from '@/api/manual/activityEvents';
import { FormSkeleton } from '@/components/Skeleton';
import {
  ACTIVITY_EVENT_DEFAULT_ENABLED,
  ACTIVITY_EVENT_TYPES,
  ACTIVITY_SEVERITIES,
  PERMISSION_NOTIFY_GROUPS,
  type ActivityEventTypeName,
  type PermissionNotifyGroupKey,
} from '@/features/activity-notifications/activityTypes';
import {
  useNotificationConfig,
  useSaveNotificationConfig,
} from '@/features/activity-notifications/hooks/useActivityNotifications';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';

type FormValues = NotificationConfig & {
  emailRecipientsText: string;
};

function recipientsToText(recipients: string[]): string {
  return recipients.join('\n');
}

function textToRecipients(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map((s) => s.trim())
    .filter((s) => s.includes('@'));
}

const PERMISSION_EVENT_SET = new Set<string>([
  ...PERMISSION_NOTIFY_GROUPS.roles,
  ...PERMISSION_NOTIFY_GROUPS.userPermissions,
  ...PERMISSION_NOTIFY_GROUPS.systemChanges,
]);

const OTHER_EVENT_TYPES = ACTIVITY_EVENT_TYPES.filter((type) => !PERMISSION_EVENT_SET.has(type));

function groupChecked(
  enabledEvents: Record<string, boolean> | undefined,
  group: PermissionNotifyGroupKey
): boolean {
  const keys = PERMISSION_NOTIFY_GROUPS[group];
  return keys.every((key) => enabledEvents?.[key] ?? ACTIVITY_EVENT_DEFAULT_ENABLED[key] ?? true);
}

function groupIndeterminate(
  enabledEvents: Record<string, boolean> | undefined,
  group: PermissionNotifyGroupKey
): boolean {
  const keys = PERMISSION_NOTIFY_GROUPS[group];
  const values = keys.map((key) => enabledEvents?.[key] ?? ACTIVITY_EVENT_DEFAULT_ENABLED[key] ?? true);
  const some = values.some(Boolean);
  const all = values.every(Boolean);
  return some && !all;
}

export function NotificationSettingsForm() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const [form] = Form.useForm<FormValues>();
  const { data: config, isLoading, isError, refetch } = useNotificationConfig(true);
  const save = useSaveNotificationConfig();
  const enabledEventsWatch = Form.useWatch('enabledEvents', form) as Record<string, boolean> | undefined;

  useEffect(() => {
    if (!config) {
      return;
    }
    const enabledEvents = ACTIVITY_EVENT_TYPES.reduce<Record<string, boolean>>((acc, eventType) => {
      const fromConfig = config.enabledEvents?.[eventType as ActivityEventTypeName];
      acc[eventType] =
        typeof fromConfig === 'boolean'
          ? fromConfig
          : (ACTIVITY_EVENT_DEFAULT_ENABLED[eventType] ?? true);
      return acc;
    }, {});

    form.setFieldsValue({
      ...config,
      emailRecipientsText: recipientsToText(config.emailRecipients ?? []),
      enabledEvents,
      severityThreshold: { ...config.severityThreshold },
    });
  }, [config, form]);

  const setPermissionGroup = (group: PermissionNotifyGroupKey, checked: boolean) => {
    const next = { ...(form.getFieldValue('enabledEvents') as Record<string, boolean> | undefined) };
    for (const key of PERMISSION_NOTIFY_GROUPS[group]) {
      next[key] = checked;
    }
    form.setFieldsValue({ enabledEvents: next });
  };

  const onFinish = (values: FormValues) => {
    const payload: NotificationConfig = {
      inAppEnabled: values.inAppEnabled,
      emailEnabled: values.emailEnabled,
      emailRecipients: textToRecipients(values.emailRecipientsText ?? ''),
      webhookEnabled: values.webhookEnabled,
      webhookUrl: values.webhookUrl?.trim() || null,
      webhookSecret: values.webhookSecret?.trim() || null,
      enabledEvents: values.enabledEvents ?? {},
      severityThreshold: values.severityThreshold ?? {},
    };
    save.mutate(payload, {
      onSuccess: () => message.success(t('activityNotifications.settingsSaved')),
      onError: () => message.error(t('activityNotifications.settingsSaveError')),
    });
  };

  if (isLoading) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <FormSkeleton fields={6} />
      </>
    );
  }

  if (isError) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <Alert
          type="error"
          title={t('activityNotifications.settingsLoadError')}
          action={
            <Button size="small" onClick={() => void refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </>
    );
  }

  return (
    <Form form={form} layout="vertical" onFinish={onFinish}>
      <Form.Item
        name="inAppEnabled"
        label={t('activityNotifications.settings.inApp')}
        valuePropName="checked"
      >
        <Switch />
      </Form.Item>

      <Divider titlePlacement="left">{t('activityNotifications.settings.emailSection')}</Divider>
      <Form.Item
        name="emailEnabled"
        label={t('activityNotifications.settings.emailEnabled')}
        valuePropName="checked"
      >
        <Switch />
      </Form.Item>
      <Form.Item
        name="emailRecipientsText"
        label={t('activityNotifications.settings.emailRecipients')}
      >
        <Input.TextArea rows={3} placeholder="ops@example.com" />
      </Form.Item>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('activityNotifications.settings.criticalEmailNote')}
      />

      <Divider titlePlacement="left">{t('activityNotifications.settings.webhookSection')}</Divider>
      <Form.Item
        name="webhookEnabled"
        label={t('activityNotifications.settings.webhookEnabled')}
        valuePropName="checked"
      >
        <Switch />
      </Form.Item>
      <Form.Item name="webhookUrl" label={t('activityNotifications.settings.webhookUrl')}>
        <Input placeholder="https://hooks.slack.com/..." />
      </Form.Item>
      <Form.Item name="webhookSecret" label={t('activityNotifications.settings.webhookSecret')}>
        <Input.Password autoComplete="off" />
      </Form.Item>

      <Divider titlePlacement="left">{t('activityNotifications.settings.permissionChangesSection')}</Divider>
      <Form.Item label={t('activityNotifications.settings.notifyOnPermissionChanges')}>
        <Space orientation="vertical" style={{ width: '100%' }}>
          {(
            [
              ['roles', 'roles'],
              ['userPermissions', 'userPermissions'],
              ['systemChanges', 'systemChanges'],
            ] as const
          ).map(([group, labelKey]) => (
            <Checkbox
              key={group}
              checked={groupChecked(enabledEventsWatch, group)}
              indeterminate={groupIndeterminate(enabledEventsWatch, group)}
              onChange={(e) => setPermissionGroup(group, e.target.checked)}
            >
              {t(`activityNotifications.settings.permissionGroups.${labelKey}`)}
            </Checkbox>
          ))}
        </Space>
      </Form.Item>

      {/* Keep individual permission event flags in the form model for save payload */}
      {([...PERMISSION_EVENT_SET] as ActivityEventTypeName[]).map((eventType) => (
        <Form.Item
          key={`hidden-${eventType}`}
          name={['enabledEvents', eventType]}
          valuePropName="checked"
          hidden
        >
          <Checkbox />
        </Form.Item>
      ))}

      <Divider titlePlacement="left">{t('activityNotifications.settings.eventsSection')}</Divider>
      <Form.Item label={t('activityNotifications.settings.enabledEvents')}>
        <Space orientation="vertical" style={{ width: '100%' }}>
          {OTHER_EVENT_TYPES.map((eventType) => (
            <Form.Item
              key={eventType}
              name={['enabledEvents', eventType]}
              valuePropName="checked"
              style={{ marginBottom: 0 }}
            >
              <Checkbox>{t(`activityNotifications.eventTypes.${eventType}`)}</Checkbox>
            </Form.Item>
          ))}
        </Space>
      </Form.Item>

      <Form.Item label={t('activityNotifications.settings.severityThreshold')}>
        <Space orientation="vertical" style={{ width: '100%' }}>
          {ACTIVITY_EVENT_TYPES.map((eventType) => (
            <Form.Item
              key={`sev-${eventType}`}
              name={['severityThreshold', eventType]}
              label={t(`activityNotifications.eventTypes.${eventType}`)}
              style={{ marginBottom: 8 }}
            >
              <Select
                allowClear
                placeholder={t('activityNotifications.settings.severityDefault')}
                options={ACTIVITY_SEVERITIES.map((s) => ({ value: s, label: s }))}
              />
            </Form.Item>
          ))}
        </Space>
      </Form.Item>

      <Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>
          {t('common.buttons.save')}
        </Button>
      </Form.Item>
    </Form>
  );
}
