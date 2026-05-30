'use client';

import { useEffect } from 'react';
import { Form, InputNumber, Switch, Button, message, Card, Typography, Select } from 'antd';
import { ClockCircleOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import { useSessionSettings, useUpdateSessionSettings } from '@/features/settings/hooks/useSessionSettings';
import type { SessionSettings as SessionSettingsValues } from '@/features/settings/api/sessionSettingsApi';

const TIMEOUT_PRESETS = [30, 60] as const;

export function SessionSettings() {
    const { t } = useI18n();
    const [form] = Form.useForm<SessionSettingsValues>();
    const { data: settings, isLoading, refetch } = useSessionSettings();
    const updateSettings = useUpdateSessionSettings();

    useEffect(() => {
        if (settings) {
            form.setFieldsValue(settings);
        }
    }, [settings, form]);

    const handleSubmit = async (values: SessionSettingsValues) => {
        try {
            await updateSettings.mutateAsync(values);
            message.success(t('settings.session.saveSuccess'));
            void refetch();
        } catch {
            message.error(t('settings.session.saveFailed'));
        }
    };

    const warningMinutes = Form.useWatch('warningMinutes', form) ?? settings?.warningMinutes ?? 1;
    const countdownSeconds = warningMinutes * 60;

    return (
        <Card
            title={
                <span>
                    <ClockCircleOutlined style={{ marginRight: 8 }} />
                    {t('settings.session.cardTitle')}
                </span>
            }
            loading={isLoading}
        >
            <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                {t('settings.session.description')}
            </Typography.Paragraph>
            <Form form={form} layout="vertical" onFinish={handleSubmit}>
                <Form.Item
                    name="enabled"
                    label={t('settings.session.enabledLabel')}
                    valuePropName="checked"
                >
                    <Switch
                        checkedChildren={t('settings.session.enabledOn')}
                        unCheckedChildren={t('settings.session.enabledOff')}
                    />
                </Form.Item>

                <Form.Item
                    name="timeoutMinutes"
                    label={t('settings.session.timeoutLabel')}
                    tooltip={t('settings.session.timeoutTooltip')}
                    rules={[
                        { required: true, message: t('settings.session.timeoutRequired') },
                        { type: 'number', min: 5, max: 480, message: t('settings.session.timeoutRange') },
                    ]}
                >
                    <Select
                        options={[
                            ...TIMEOUT_PRESETS.map((m) => ({
                                value: m,
                                label: t('settings.session.timeoutPresetMinutes', { minutes: m }),
                            })),
                            { value: 45, label: '45' },
                            { value: 120, label: '120' },
                        ]}
                        style={{ maxWidth: 280 }}
                    />
                </Form.Item>

                <Form.Item
                    name="warningMinutes"
                    label={t('settings.session.warningLabel')}
                    tooltip={t('settings.session.warningTooltip')}
                    rules={[
                        { required: true, message: t('settings.session.warningRequired') },
                        { type: 'number', min: 1, max: 10, message: t('settings.session.warningRange') },
                    ]}
                    extra={t('settings.session.warningCountdownHint', { seconds: String(countdownSeconds) })}
                >
                    <InputNumber min={1} max={10} style={{ width: 128 }} addonAfter={t('settings.session.minutesUnit')} />
                </Form.Item>

                <Form.Item>
                    <Button type="primary" htmlType="submit" loading={updateSettings.isPending}>
                        {t('settings.session.save')}
                    </Button>
                </Form.Item>
            </Form>
        </Card>
    );
}
