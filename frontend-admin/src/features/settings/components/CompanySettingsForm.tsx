'use client';

import { Alert, Button, Card, Form, Input, Space, Spin, Typography } from 'antd';
import Link from 'next/link';
import { useEffect } from 'react';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { useCompanySettings, useUpdateCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import {
    mapCompanyFormToUpdateRequest,
    mapCompanySettingsToFormValues,
    type CompanySettingsFormValues,
} from '@/features/settings/types/companySettingsForm';

const ATU_REGEX = /^ATU\d{8}$/;

function getLoadErrorDescription(err: unknown, translate: (key: string) => string): string {
    if (err instanceof Error && err.message.trim()) return err.message.trim();
    const normalized = (err as { normalized?: { message?: string } })?.normalized;
    if (normalized?.message?.trim()) return normalized.message.trim();
    const msg = (err as { message?: string })?.message;
    if (typeof msg === 'string' && msg.trim()) return msg.trim();
    return translate('settings.page.loadErrorFallback');
}

export function CompanySettingsForm() {
    const { t } = useI18n();
    const { message } = useAntdApp();
    const [form] = Form.useForm<CompanySettingsFormValues>();
    const { data: settings, isLoading, isError, error, refetch, isFetching, isSuccess } = useCompanySettings();
    const { updateSettings, isLoading: isUpdating } = useUpdateCompanySettings();

    useEffect(() => {
        if (settings) {
            form.setFieldsValue(mapCompanySettingsToFormValues(settings));
        }
    }, [settings, form]);

    const onFinish = async (values: CompanySettingsFormValues) => {
        try {
            const payload = mapCompanyFormToUpdateRequest(values, settings);
            await updateSettings(payload);
            message.success(t('settings.companyPage.saveSuccess'));
            await refetch();
        } catch {
            message.error(t('settings.page.saveFailed'));
        }
    };

    if (isLoading) {
        return (
            <Card>
                <div style={{ textAlign: 'center', padding: '48px 24px' }}>
                    <Spin size="large" />
                    <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
                        {t('settings.page.loading')}
                    </Typography.Paragraph>
                </div>
            </Card>
        );
    }

    if (isError) {
        return (
            <Alert
                type="error"
                title={t('settings.page.loadErrorTitle')}
                description={getLoadErrorDescription(error, t)}
                showIcon
                action={
                    <Button size="small" type="primary" onClick={() => refetch()} loading={isFetching}>
                        {t('common.buttons.retry')}
                    </Button>
                }
            />
        );
    }

    if (isSuccess && settings == null) {
        return (
            <Card>
                <Typography.Paragraph type="secondary">{t('settings.page.empty')}</Typography.Paragraph>
                <Button type="primary" onClick={() => refetch()} loading={isFetching}>
                    {t('common.buttons.retry')}
                </Button>
            </Card>
        );
    }

    const g = (key: string) => t(`settings.form.general.${key}`);
    const c = (key: string) => t(`settings.companyPage.${key}`);

    return (
        <Card title={c('cardTitle')}>
            <Alert
                type="info"
                showIcon
                title={c('rksvInfoTitle')}
                description={c('rksvInfoDescription')}
                style={{ marginBottom: 24 }}
            />

            <Form form={form} layout="vertical" onFinish={onFinish}>
                <Alert
                    type="warning"
                    showIcon
                    title={c('requiredFieldsTitle')}
                    description={c('requiredFieldsDescription')}
                    style={{ marginBottom: 16 }}
                />

                <Form.Item
                    name="companyName"
                    label={g('companyName')}
                    rules={[{ required: true, message: g('companyNameRequired') }]}
                >
                    <Input placeholder={c('placeholderCompanyName')} />
                </Form.Item>

                <Form.Item
                    name="companyAddress"
                    label={g('companyAddress')}
                    rules={[{ required: true, message: g('companyAddressRequired') }]}
                >
                    <Input.TextArea rows={3} placeholder={c('placeholderAddress')} />
                </Form.Item>

                <Form.Item
                    name="companyTaxNumber"
                    label={g('companyTaxNumber')}
                    rules={[
                        { required: true, message: g('companyTaxNumberRequired') },
                        { pattern: ATU_REGEX, message: g('companyTaxNumberPattern') },
                    ]}
                >
                    <Input placeholder={g('placeholderAtu')} />
                </Form.Item>

                <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 16 }}>
                    {c('optionalSectionTitle')}
                </Typography.Title>

                <Form.Item name="companyPhone" label={c('phone')}>
                    <Input placeholder={c('placeholderPhone')} />
                </Form.Item>

                <Form.Item
                    name="companyEmail"
                    label={c('email')}
                    rules={[{ type: 'email', message: g('contactEmailInvalid') }]}
                >
                    <Input type="email" placeholder={c('placeholderEmail')} />
                </Form.Item>

                <Form.Item name="companyWebsite" label={g('companyWebsite')}>
                    <Input placeholder={c('placeholderWebsite')} />
                </Form.Item>

                <Form.Item name="companyDescription" label={c('receiptFooter')}>
                    <Input.TextArea rows={2} placeholder={c('placeholderReceiptFooter')} />
                </Form.Item>

                <Form.Item>
                    <Space wrap>
                        <Button type="primary" htmlType="submit" loading={isUpdating}>
                            {t('settings.page.saveChanges')}
                        </Button>
                        <Button onClick={() => form.setFieldsValue(mapCompanySettingsToFormValues(settings))}>
                            {c('reset')}
                        </Button>
                    </Space>
                </Form.Item>
            </Form>

            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8 }}>
                {c('advancedHint')}{' '}
                <Link href="/settings">{c('advancedLink')}</Link>
            </Typography.Paragraph>
        </Card>
    );
}
