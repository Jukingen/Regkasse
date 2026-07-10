'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect } from 'react';
import { Alert, Button, Checkbox, Divider, Form, Input, Select, Space, Spin, Switch } from 'antd';

import type { NotificationConfig } from '@/api/manual/activityEvents';
import {
    ACTIVITY_EVENT_TYPES,
    ACTIVITY_SEVERITIES,
    type ActivityEventTypeName,
} from '@/features/activity-notifications/activityTypes';
import {
    useNotificationConfig,
    useSaveNotificationConfig,
} from '@/features/activity-notifications/hooks/useActivityNotifications';
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

export function NotificationSettingsForm() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [form] = Form.useForm<FormValues>();
    const { data: config, isLoading, isError, refetch } = useNotificationConfig(true);
    const save = useSaveNotificationConfig();

    useEffect(() => {
        if (!config) {
            return;
        }
        const enabledEvents = ACTIVITY_EVENT_TYPES.reduce<Record<string, boolean>>((acc, eventType) => {
            acc[eventType] = config.enabledEvents?.[eventType as ActivityEventTypeName] ?? true;
            return acc;
        }, {});

        form.setFieldsValue({
            ...config,
            emailRecipientsText: recipientsToText(config.emailRecipients ?? []),
            enabledEvents,
            severityThreshold: { ...config.severityThreshold },
        });
    }, [config, form]);

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
                <div style={{ padding: 48, textAlign: 'center' }}>
                    <Spin />
                </div>
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
            <Form.Item name="inAppEnabled" label={t('activityNotifications.settings.inApp')} valuePropName="checked">
                <Switch />
            </Form.Item>

            <Divider titlePlacement="left">{t('activityNotifications.settings.emailSection')}</Divider>
            <Form.Item name="emailEnabled" label={t('activityNotifications.settings.emailEnabled')} valuePropName="checked">
                <Switch />
            </Form.Item>
            <Form.Item name="emailRecipientsText" label={t('activityNotifications.settings.emailRecipients')}>
                <Input.TextArea rows={3} placeholder="ops@example.com" />
            </Form.Item>

            <Divider titlePlacement="left">{t('activityNotifications.settings.webhookSection')}</Divider>
            <Form.Item name="webhookEnabled" label={t('activityNotifications.settings.webhookEnabled')} valuePropName="checked">
                <Switch />
            </Form.Item>
            <Form.Item name="webhookUrl" label={t('activityNotifications.settings.webhookUrl')}>
                <Input placeholder="https://hooks.slack.com/..." />
            </Form.Item>
            <Form.Item name="webhookSecret" label={t('activityNotifications.settings.webhookSecret')}>
                <Input.Password autoComplete="off" />
            </Form.Item>

            <Divider titlePlacement="left">{t('activityNotifications.settings.eventsSection')}</Divider>
            <Form.Item label={t('activityNotifications.settings.enabledEvents')}>
                <Space orientation="vertical" style={{ width: '100%' }}>
                    {ACTIVITY_EVENT_TYPES.map((eventType) => (
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
