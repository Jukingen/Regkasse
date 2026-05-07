'use client';

import React, { useEffect, useState } from 'react';
import { Form, Input, Button, Card, Tabs, message, Row, Col, InputNumber, Switch, Divider, Spin, Descriptions, Typography, Alert, Empty, Badge, Modal } from 'antd';
import { SaveOutlined, LockOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    useGetApiCompanySettings,
    usePutApiCompanySettings,
} from '@/api/generated/company-settings/company-settings';
import {
    mapSettingsToFormValues,
    mapFormValuesToUpdateRequest,
    type SettingsFormValues,
} from '@/features/settings/types/settingsForm';
import type { CompanySettings } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { LanguageSelector } from '@/features/settings/components/LanguageSelector';

const ATU_REGEX = /^ATU\d{8}$/;

/** User-facing load error text; avoids dumping unknown error shapes raw into the UI */
function getSettingsLoadErrorDescription(err: unknown, translate: (key: string) => string): string {
    if (err instanceof Error && err.message.trim()) return err.message.trim();
    const normalized = (err as { normalized?: { message?: string } })?.normalized;
    if (normalized?.message?.trim()) return normalized.message.trim();
    const msg = (err as { message?: string })?.message;
    if (typeof msg === 'string' && msg.trim()) return msg.trim();
    return translate('settings.page.loadErrorFallback');
}

export default function SettingsPage() {
    const { t } = useI18n();
    const { data: settings, isLoading, isError, error, refetch, isFetching, isSuccess } = useGetApiCompanySettings();
    const updateMutation = usePutApiCompanySettings();
    const [form] = Form.useForm<SettingsFormValues>();

    useEffect(() => {
        if (settings) {
            form.setFieldsValue(mapSettingsToFormValues(settings) as SettingsFormValues);
        }
    }, [settings, form]);

    const handleSave = async (values: SettingsFormValues) => {
        try {
            const payload = mapFormValuesToUpdateRequest(values);
            await updateMutation.mutateAsync({ data: payload });
            message.success(t('settings.page.saveChanges'));
        } catch (err) {
            message.error(t('settings.page.saveFailed'));
        }
    };

    const headerBreadcrumbs = [adminOverviewCrumb(t), { title: t('settings.page.title') }];

    if (isLoading) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title={t('settings.page.title')} breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <div style={{ textAlign: 'center', padding: '48px 24px' }}>
                        <Spin size="large" />
                        <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
                            {t('settings.page.loading')}
                        </Typography.Paragraph>
                    </div>
                </Card>
            </SpaceWrapper>
        );
    }

    if (isError) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title={t('settings.page.title')} breadcrumbs={[...headerBreadcrumbs]} />
                <Alert
                    type="error"
                    message={t('settings.page.loadErrorTitle')}
                    description={getSettingsLoadErrorDescription(error, t)}
                    showIcon
                    action={
                        <Button size="small" type="primary" onClick={() => refetch()} loading={isFetching}>
                            {t('common.buttons.retry')}
                        </Button>
                    }
                />
            </SpaceWrapper>
        );
    }

    if (isSuccess && (settings as CompanySettings | null) == null) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title={t('settings.page.title')} breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description={t('settings.page.empty')}
                    >
                        <Button type="primary" onClick={() => refetch()} loading={isFetching}>
                            {t('common.buttons.retry')}
                        </Button>
                    </Empty>
                </Card>
            </SpaceWrapper>
        );
    }

    return (
        <SpaceWrapper>
            <AdminPageHeader
                title={t('settings.page.title')}
                breadcrumbs={[...headerBreadcrumbs]}
                actions={
                    <Button
                        type="primary"
                        icon={<SaveOutlined />}
                        onClick={() => form.submit()}
                        loading={updateMutation.isPending}
                    >
                        {t('settings.page.saveChanges')}
                    </Button>
                }
            />

            <Form<SettingsFormValues>
                form={form}
                layout="vertical"
                onFinish={handleSave}
            >
                <Tabs
                    defaultActiveKey="1"
                    items={[
                        {
                            key: '1',
                            label: t('settings.tabs.general'),
                            children: <GeneralInfoTab />,
                        },
                        {
                            key: '2',
                            label: t('settings.tabs.localization'),
                            children: <LocalizationTab />,
                        },
                        {
                            key: '3',
                            label: t('settings.tabs.finanzOnline'),
                            children: <FinanzOnlineTab />,
                        },
                        {
                            key: '4',
                            label: t('settings.tabs.tse'),
                            children: <TSETab />,
                        },
                        {
                            key: '5',
                            label: t('settings.tabs.password'),
                            icon: <LockOutlined />,
                            children: <ChangeMyPasswordTab />,
                        },
                        {
                            key: '6',
                            label: 'Demo Reset',
                            children: <DemoResetTab />,
                        },
                    ]}
                />
            </Form>
        </SpaceWrapper>
    );
}

function SpaceWrapper({ children }: { children: React.ReactNode }) {
    return <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>{children}</div>;
}

function GeneralInfoTab() {
    const { t } = useI18n();
    const g = (key: string) => t(`settings.form.general.${key}`);
    return (
        <Card title={g('cardTitle')}>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label={g('companyName')}
                        name="companyName"
                        rules={[{ required: true, message: g('companyNameRequired') }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label={g('companyAddress')}
                        name="companyAddress"
                        rules={[{ required: true, message: g('companyAddressRequired') }]}
                    >
                        <Input.TextArea rows={3} />
                    </Form.Item>
                    <Form.Item
                        label={g('companyTaxNumber')}
                        name="companyTaxNumber"
                        rules={[
                            { required: true, message: g('companyTaxNumberRequired') },
                            { pattern: ATU_REGEX, message: g('companyTaxNumberPattern') },
                        ]}
                    >
                        <Input placeholder={g('placeholderAtu')} />
                    </Form.Item>
                    <Form.Item
                        label={g('companyVatNumber')}
                        name="companyVatNumber"
                        rules={[{ pattern: ATU_REGEX, message: g('companyTaxNumberPattern') }]}
                    >
                        <Input placeholder={g('placeholderAtu')} />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label={g('contactPerson')} name="contactPerson">
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label={g('contactEmail')}
                        name="contactEmail"
                        rules={[{ type: 'email', message: g('contactEmailInvalid') }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item label={g('contactPhone')} name="contactPhone">
                        <Input />
                    </Form.Item>
                    <Form.Item label={g('companyWebsite')} name="companyWebsite">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>

            <Divider />

            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label={g('bankName')} name="bankName">
                        <Input />
                    </Form.Item>
                    <Form.Item label={g('bankAccountNumber')} name="bankAccountNumber">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label={g('bankSwiftCode')} name="bankSwiftCode">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
        </Card>
    );
}

function LocalizationTab() {
    const { t } = useI18n();
    const l = (key: string) => t(`settings.form.localization.${key}`);
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <Card title={t('settings.language.cardTitle')}>
                <LanguageSelector />
            </Card>
            <Card title={l('cardTitle')}>
            <Row gutter={24}>
                <Col span={8}>
                    <Form.Item
                        label={l('defaultLanguage')}
                        name="defaultLanguage"
                        rules={[{ required: true, message: l('defaultLanguageRequired') }]}
                    >
                        <Input placeholder={l('placeholderDefaultLanguage')} />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label={l('defaultCurrency')}
                        name="defaultCurrency"
                        rules={[{ required: true, message: l('defaultCurrencyRequired') }]}
                    >
                        <Input placeholder={l('placeholderCurrency')} />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label={l('defaultTimeZone')}
                        name="defaultTimeZone"
                        rules={[{ required: true, message: l('defaultTimeZoneRequired') }]}
                    >
                        <Input placeholder={l('placeholderTimeZone')} />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label={l('defaultDateFormat')}
                        name="defaultDateFormat"
                        rules={[{ required: true, message: l('defaultDateFormatRequired') }]}
                    >
                        <Input placeholder={l('placeholderDateFormat')} />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label={l('defaultTimeFormat')}
                        name="defaultTimeFormat"
                        rules={[{ required: true, message: l('defaultTimeFormatRequired') }]}
                    >
                        <Input placeholder={l('placeholderTimeFormat')} />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label={l('receiptNumbering')}
                        name="receiptNumbering"
                        rules={[{ required: true, message: l('receiptNumberingRequired') }]}
                    >
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label={l('invoiceNumbering')}
                        name="invoiceNumbering"
                        rules={[{ required: true, message: l('invoiceNumberingRequired') }]}
                    >
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
            </Card>
        </div>
    );
}

function FinanzOnlineTab() {
    const { t } = useI18n();
    const f = (key: string) => t(`settings.form.finanzOnline.${key}`);
    const empty = t('settings.display.emptyValue');
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <Card title={f('credentialsCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnabled" valuePropName="checked" label={f('integrationEnabled')}>
                            <Switch />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label={f('apiUrl')}
                            name="finanzOnlineApiUrl"
                            rules={[
                                { max: 500, message: f('apiUrlMax') },
                                { type: 'url', message: f('apiUrlInvalid') },
                            ]}
                        >
                            <Input placeholder={f('placeholderApiUrl')} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label={f('participantId')}
                            name="finanzOnlineParticipantId"
                            rules={[{ max: 100, message: f('participantIdMax') }]}
                            extra={f('participantIdExtra')}
                        >
                            <Input placeholder={f('placeholderParticipantId')} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label={f('pin')}
                            name="finanzOnlinePin"
                            rules={[{ max: 100, message: f('pinMax') }]}
                            extra={f('pinExtra')}
                        >
                            <Input.Password placeholder={f('placeholderPin')} />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={f('deliveryCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label={f('sessionTimeout')}
                            name="finanzOnlineSubmitInterval"
                            rules={[{ type: 'number', min: 1, max: 1440, message: f('sessionTimeoutRange') }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={1} max={1440} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineAutoSubmit" valuePropName="checked" label={f('autoSubmit')}>
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={f('validationCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label={f('retryAttempts')}
                            name="finanzOnlineRetryAttempts"
                            rules={[{ type: 'number', min: 0, max: 20, message: f('retryAttemptsRange') }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={0} max={20} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnableValidation" valuePropName="checked" label={f('payloadValidationEnabled')}>
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={f('runtimeCardTitle')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    {f('runtimeCardDescription')}
                </Typography.Paragraph>
                <Descriptions size="small" bordered column={1}>
                    <Descriptions.Item label={f('lastSyncLabel')}>
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('lastFinanzOnlineSync');
                                return v ? String(v) : empty;
                            }}
                        </Form.Item>
                    </Descriptions.Item>
                    <Descriptions.Item label={f('pendingInvoicesLabel')}>
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('pendingInvoices');
                                return typeof v === 'number' ? String(v) : empty;
                            }}
                        </Form.Item>
                    </Descriptions.Item>
                </Descriptions>
            </Card>
        </div>
    );
}

function TSETab() {
    const { t } = useI18n();
    const ts = (key: string) => t(`settings.form.tse.${key}`);
    return (
        <Card title={ts('cardTitle')}>
            <Form.Item name="tseAutoConnect" valuePropName="checked" label={ts('autoConnect')}>
                <Switch />
            </Form.Item>

            <Form.Item
                label={ts('defaultDeviceId')}
                name="defaultTseDeviceId"
                rules={[{ max: 100, message: ts('defaultDeviceIdMax') }]}
            >
                <Input />
            </Form.Item>

            <Form.Item
                label={ts('connectionTimeout')}
                name="tseConnectionTimeout"
                rules={[{ type: 'number', min: 5, max: 120000, message: ts('connectionTimeoutRange') }]}
            >
                <InputNumber style={{ width: '100%' }} min={5} max={120000} />
            </Form.Item>
        </Card>
    );
}

function ChangeMyPasswordTab() {
    const { t } = useI18n();
    const [form] = Form.useForm<{ currentPassword: string; newPassword: string; confirmPassword: string }>();
    const [loading, setLoading] = useState(false);

    const onFinish = async (values: { currentPassword: string; newPassword: string; confirmPassword: string }) => {
        if (values.newPassword !== values.confirmPassword) {
            form.setFields([{ name: 'confirmPassword', errors: [t('settings.changePassword.confirmMismatch')] }]);
            return;
        }
        setLoading(true);
        try {
            await customInstance<{ message?: string }>({
                url: '/api/UserManagement/me/password',
                method: 'PUT',
                data: { currentPassword: values.currentPassword, newPassword: values.newPassword },
            });
            message.success(t('settings.changePassword.success'));
            form.resetFields();
        } catch (err: unknown) {
            const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
            message.error(msg ?? t('settings.changePassword.errorFallback'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <Card title={t('settings.changePassword.title')}>
            <Form form={form} layout="vertical" onFinish={onFinish} style={{ maxWidth: 400 }}>
                <Form.Item
                    name="currentPassword"
                    label={t('settings.changePassword.currentPassword')}
                    rules={[{ required: true, message: t('settings.changePassword.currentPasswordRequired') }]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="current-password" />
                </Form.Item>
                <Form.Item
                    name="newPassword"
                    label={t('settings.changePassword.newPassword')}
                    rules={[
                        { required: true, message: t('settings.changePassword.newPasswordRequired') },
                        { min: 8, message: t('settings.changePassword.minLength') },
                    ]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="new-password" />
                </Form.Item>
                <Form.Item
                    name="confirmPassword"
                    label={t('settings.changePassword.confirmPassword')}
                    dependencies={['newPassword']}
                    rules={[
                        { required: true, message: t('settings.changePassword.confirmRequired') },
                        ({ getFieldValue }) => ({
                            validator(_, value) {
                                if (!value || getFieldValue('newPassword') === value) return Promise.resolve();
                                return Promise.reject(new Error(t('settings.changePassword.confirmMismatch')));
                            },
                        }),
                    ]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="new-password" />
                </Form.Item>
                <Form.Item>
                    <Button type="primary" htmlType="submit" loading={loading} icon={<LockOutlined />}>
                        {t('settings.changePassword.submit')}
                    </Button>
                </Form.Item>
            </Form>
        </Card>
    );
}

type DemoResetApiResponse = {
    success?: boolean;
    message?: string;
    resetAt?: string;
};

function DemoResetTab() {
    const [confirmOpen, setConfirmOpen] = useState(false);
    const [confirmText, setConfirmText] = useState('');
    const isDevelopment = process.env.NODE_ENV !== 'production';
    const canReset = isDevelopment;

    const resetMutation = useMutation({
        mutationFn: async () => {
            const response = await customInstance<DemoResetApiResponse>({
                url: '/api/admin/demo/reset',
                method: 'POST',
            });
            return response.data;
        },
        onSuccess: () => {
            message.success('Demo reset completed. Bitte Seite neu laden.');
            setConfirmOpen(false);
            setConfirmText('');
        },
        onError: (err: unknown) => {
            const apiMessage =
                (err as { response?: { data?: { message?: string; detail?: string; title?: string } } })?.response?.data?.message
                ?? (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail
                ?? (err as { response?: { data?: { title?: string } } })?.response?.data?.title
                ?? (err as Error)?.message
                ?? 'Demo reset failed.';
            message.error(`Demo reset failed: ${apiMessage}`);
        },
    });

    const openConfirmation = () => {
        setConfirmText('');
        setConfirmOpen(true);
    };

    const handleConfirm = async () => {
        if (confirmText.trim() !== 'RESET') {
            message.error("Bitte 'RESET' eingeben, um fortzufahren.");
            return;
        }

        await resetMutation.mutateAsync();
    };

    return (
        <Card title="Demo Database Reset">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div>
                    <Badge
                        status={canReset ? 'processing' : 'default'}
                        text={canReset ? 'Environment: Development (reset allowed)' : 'Environment: Production (reset disabled)'}
                    />
                </div>

                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Dieser Vorgang löscht Zahlungs-, Beleg- und Gutschein-Daten und erstellt eine neue Demo-Kasse samt Startbeleg.
                </Typography.Paragraph>

                <div>
                    <Button
                        danger
                        type="primary"
                        onClick={openConfirmation}
                        disabled={!canReset}
                        loading={resetMutation.isPending}
                    >
                        Demo Database Reset
                    </Button>
                </div>
            </div>

            <Modal
                title="Demo Database Reset bestätigen"
                open={confirmOpen}
                onCancel={() => {
                    if (!resetMutation.isPending) {
                        setConfirmOpen(false);
                        setConfirmText('');
                    }
                }}
                confirmLoading={resetMutation.isPending}
                onOk={() => void handleConfirm()}
                okText="Reset ausführen"
                okButtonProps={{ danger: true }}
                cancelText="Abbrechen"
                maskClosable={!resetMutation.isPending}
                closable={!resetMutation.isPending}
            >
                <Typography.Paragraph>
                    Are you sure? This will delete ALL payments, receipts, vouchers, and reset the cash register.
                    This cannot be undone. Type 'RESET' to confirm.
                </Typography.Paragraph>
                <Input
                    autoFocus
                    value={confirmText}
                    onChange={(e) => setConfirmText(e.target.value)}
                    placeholder="Type RESET"
                    disabled={resetMutation.isPending}
                />
            </Modal>
        </Card>
    );
}
