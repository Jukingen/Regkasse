'use client';

import React, { useEffect, useState } from 'react';
import { Form, Input, Button, Card, Tabs, message, Row, Col, InputNumber, Switch, Divider, Spin, Descriptions, Typography, Alert, Empty } from 'antd';
import { SaveOutlined, LockOutlined } from '@ant-design/icons';
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
import { useI18n } from '@/i18n/I18nProvider';

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

    const headerBreadcrumbs = [adminOverviewCrumb(t), { title: t('nav.settings') }];

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
    return (
        <Card title={t('settings.form.general.cardTitle')}>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Firmenname"
                        name="companyName"
                        rules={[{ required: true, message: 'Firmenname ist erforderlich' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label="Firmenadresse"
                        name="companyAddress"
                        rules={[{ required: true, message: 'Firmenadresse ist erforderlich' }]}
                    >
                        <Input.TextArea rows={3} />
                    </Form.Item>
                    <Form.Item
                        label="Steuernummer (ATU)"
                        name="companyTaxNumber"
                        rules={[
                            { required: true, message: 'Steuernummer ist erforderlich' },
                            { pattern: ATU_REGEX, message: 'Format: ATU + 8 Ziffern' },
                        ]}
                    >
                        <Input placeholder="ATU12345678" />
                    </Form.Item>
                    <Form.Item
                        label="USt-IdNr. (ATU)"
                        name="companyVatNumber"
                        rules={[{ pattern: ATU_REGEX, message: 'Format: ATU + 8 Ziffern' }]}
                    >
                        <Input placeholder="ATU12345678" />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Ansprechpartner" name="contactPerson">
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label="Kontakt-E-Mail"
                        name="contactEmail"
                        rules={[{ type: 'email', message: 'Ungültige E-Mail-Adresse' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item label="Kontakt-Telefon" name="contactPhone">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Website" name="companyWebsite">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>

            <Divider />

            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label="Bankname" name="bankName">
                        <Input />
                    </Form.Item>
                    <Form.Item label="IBAN" name="bankAccountNumber">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="BIC" name="bankSwiftCode">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
        </Card>
    );
}

function LocalizationTab() {
    const { t } = useI18n();
    return (
        <Card title={t('settings.form.localization.cardTitle')}>
            <Row gutter={24}>
                <Col span={8}>
                    <Form.Item
                        label="Standardsprache"
                        name="defaultLanguage"
                        rules={[{ required: true, message: 'Standardsprache ist erforderlich' }]}
                    >
                        <Input placeholder="de-DE" />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label="Währung"
                        name="defaultCurrency"
                        rules={[{ required: true, message: 'Währung ist erforderlich' }]}
                    >
                        <Input placeholder="EUR" />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label="Zeitzone"
                        name="defaultTimeZone"
                        rules={[{ required: true, message: 'Zeitzone ist erforderlich' }]}
                    >
                        <Input placeholder="Europe/Vienna" />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Datumsformat"
                        name="defaultDateFormat"
                        rules={[{ required: true, message: 'Datumsformat ist erforderlich' }]}
                    >
                        <Input placeholder="dd.MM.yyyy" />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label="Zeitformat"
                        name="defaultTimeFormat"
                        rules={[{ required: true, message: 'Zeitformat ist erforderlich' }]}
                    >
                        <Input placeholder="HH:mm:ss" />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Belegnummerierung"
                        name="receiptNumbering"
                        rules={[{ required: true, message: 'Belegnummerierung ist erforderlich' }]}
                    >
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label="Rechnungsnummerierung"
                        name="invoiceNumbering"
                        rules={[{ required: true, message: 'Rechnungsnummerierung ist erforderlich' }]}
                    >
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
        </Card>
    );
}

function FinanzOnlineTab() {
    const { t } = useI18n();
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <Card title={t('settings.form.finanzOnline.credentialsCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnabled" valuePropName="checked" label="FinanzOnline-Integration aktiv">
                            <Switch />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="API-URL"
                            name="finanzOnlineApiUrl"
                            rules={[
                                { max: 500, message: 'Maximal 500 Zeichen' },
                                { type: 'url', message: 'Bitte eine gültige URL angeben' },
                            ]}
                        >
                            <Input placeholder="https://finanzonline.example.at/api" />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Teilnehmer-ID"
                            name="finanzOnlineParticipantId"
                            rules={[{ max: 100, message: 'Maximal 100 Zeichen' }]}
                            extra="Teilnehmer-ID oder kombinierte Teilnehmer-/Benutzer-ID je nach Anbieter."
                        >
                            <Input placeholder="Teilnehmer-ID" />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="PIN"
                            name="finanzOnlinePin"
                            rules={[{ max: 100, message: 'Maximal 100 Zeichen' }]}
                            extra="Aus Sicherheitsgründen wird ein bestehender PIN nie angezeigt. Feld leer lassen, um den aktuellen PIN beizubehalten."
                        >
                            <Input.Password placeholder="PIN" />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={t('settings.form.finanzOnline.deliveryCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Sitzungs-Timeout (Min.)"
                            name="finanzOnlineSubmitInterval"
                            rules={[{ type: 'number', min: 1, max: 1440, message: 'Wert zwischen 1 und 1440' }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={1} max={1440} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineAutoSubmit" valuePropName="checked" label="Automatische Übermittlung">
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={t('settings.form.finanzOnline.validationCardTitle')}>
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Wiederholungsversuche"
                            name="finanzOnlineRetryAttempts"
                            rules={[{ type: 'number', min: 0, max: 20, message: 'Wert zwischen 0 und 20' }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={0} max={20} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnableValidation" valuePropName="checked" label="Payload-Validierung aktiv">
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title={t('settings.form.finanzOnline.runtimeCardTitle')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    {t('settings.form.finanzOnline.runtimeCardDescription')}
                </Typography.Paragraph>
                <Descriptions size="small" bordered column={1}>
                    <Descriptions.Item label={t('settings.form.finanzOnline.lastSyncLabel')}>
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('lastFinanzOnlineSync');
                                return v ? String(v) : '—';
                            }}
                        </Form.Item>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('settings.form.finanzOnline.pendingInvoicesLabel')}>
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('pendingInvoices');
                                return typeof v === 'number' ? String(v) : '—';
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
    return (
        <Card title={t('settings.form.tse.cardTitle')}>
            <Form.Item name="tseAutoConnect" valuePropName="checked" label="Beim Start automatisch verbinden">
                <Switch />
            </Form.Item>

            <Form.Item
                label="Standard-TSE-Geräte-ID"
                name="defaultTseDeviceId"
                rules={[{ max: 100, message: 'Maximal 100 Zeichen' }]}
            >
                <Input />
            </Form.Item>

            <Form.Item
                label="Verbindungs-Timeout (ms)"
                name="tseConnectionTimeout"
                rules={[{ type: 'number', min: 5, max: 120000, message: 'Wert zwischen 5 und 120000' }]}
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
