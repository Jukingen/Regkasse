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
import { customInstance } from '@/lib/axios';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';

const ATU_REGEX = /^ATU\d{8}$/;

/** User-facing load error text; avoids dumping unknown error shapes raw into the UI */
function getSettingsLoadErrorDescription(err: unknown): string {
    if (err instanceof Error && err.message.trim()) return err.message.trim();
    const normalized = (err as { normalized?: { message?: string } })?.normalized;
    if (normalized?.message?.trim()) return normalized.message.trim();
    const msg = (err as { message?: string })?.message;
    if (typeof msg === 'string' && msg.trim()) return msg.trim();
    return 'Firmeneinstellungen konnten nicht geladen werden. Verbindung prüfen und erneut versuchen.';
}

export default function SettingsPage() {
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
            message.success('Einstellungen gespeichert.');
        } catch (err) {
            message.error('Speichern fehlgeschlagen.');
        }
    };

    const headerBreadcrumbs = [ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.settings }];

    if (isLoading) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title="Firmeneinstellungen" breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <div style={{ textAlign: 'center', padding: '48px 24px' }}>
                        <Spin size="large" />
                        <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
                            Firmeneinstellungen werden geladen…
                        </Typography.Paragraph>
                    </div>
                </Card>
            </SpaceWrapper>
        );
    }

    if (isError) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title="Firmeneinstellungen" breadcrumbs={[...headerBreadcrumbs]} />
                <Alert
                    type="error"
                    message="Firmeneinstellungen konnten nicht geladen werden"
                    description={getSettingsLoadErrorDescription(error)}
                    showIcon
                    action={
                        <Button size="small" type="primary" onClick={() => refetch()} loading={isFetching}>
                            Retry
                        </Button>
                    }
                />
            </SpaceWrapper>
        );
    }

    if (isSuccess && settings == null) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title="Firmeneinstellungen" breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description="Es wurden keine Firmeneinstellungen zurückgegeben."
                    >
                        <Button type="primary" onClick={() => refetch()} loading={isFetching}>
                            Erneut laden
                        </Button>
                    </Empty>
                </Card>
            </SpaceWrapper>
        );
    }

    return (
        <SpaceWrapper>
            <AdminPageHeader
                title="Firmeneinstellungen"
                breadcrumbs={[...headerBreadcrumbs]}
                actions={
                    <Button
                        type="primary"
                        icon={<SaveOutlined />}
                        onClick={() => form.submit()}
                        loading={updateMutation.isPending}
                    >
                        Änderungen speichern
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
                            label: 'General Information',
                            children: <GeneralInfoTab />,
                        },
                        {
                            key: '2',
                            label: 'Region & Formatierung',
                            children: <LocalizationTab />,
                        },
                        {
                            key: '3',
                            label: 'FinanzOnline',
                            children: <FinanzOnlineTab />,
                        },
                        {
                            key: '4',
                            label: 'TSE',
                            children: <TSETab />,
                        },
                        {
                            key: '5',
                            label: 'Mein Passwort',
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
    return (
        <Card title="Unternehmen & Kontakt">
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
    return (
        <Card title="Region & Formatierung">
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
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <Card title="Zugangsdaten & Anbindung">
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

            <Card title="Übermittlung">
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

            <Card title="Validierung & Wiederholungen">
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

            <Card title="Laufzeitstatus (nur Anzeige)">
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    Laufzeitstatus aus dem Backend. Diese Felder sind schreibgeschützt und werden nicht mit „Änderungen speichern“ geschrieben.
                </Typography.Paragraph>
                <Descriptions size="small" bordered column={1}>
                    <Descriptions.Item label="Letzte FinanzOnline-Synchronisation">
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('lastFinanzOnlineSync');
                                return v ? String(v) : '—';
                            }}
                        </Form.Item>
                    </Descriptions.Item>
                    <Descriptions.Item label="Ausstehende Rechnungen">
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
    return (
        <Card title="TSE (Technische Sicherheitseinrichtung)">
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

const changePasswordCopy = {
    title: 'Mein Passwort ändern',
    currentPassword: 'Aktuelles Passwort',
    newPassword: 'Neues Passwort (min. 8 Zeichen, Groß-/Kleinbuchstaben, Zahl, Sonderzeichen)',
    confirmPassword: 'Neues Passwort bestätigen',
    submit: 'Passwort ändern',
    success: 'Passwort wurde geändert.',
    confirmMismatch: 'Passwörter stimmen nicht überein.',
};

function ChangeMyPasswordTab() {
    const [form] = Form.useForm<{ currentPassword: string; newPassword: string; confirmPassword: string }>();
    const [loading, setLoading] = useState(false);

    const onFinish = async (values: { currentPassword: string; newPassword: string; confirmPassword: string }) => {
        if (values.newPassword !== values.confirmPassword) {
            form.setFields([{ name: 'confirmPassword', errors: [changePasswordCopy.confirmMismatch] }]);
            return;
        }
        setLoading(true);
        try {
            await customInstance<{ message?: string }>({
                url: '/api/UserManagement/me/password',
                method: 'PUT',
                data: { currentPassword: values.currentPassword, newPassword: values.newPassword },
            });
            message.success(changePasswordCopy.success);
            form.resetFields();
        } catch (err: unknown) {
            const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
            message.error(msg ?? 'Passwort konnte nicht geändert werden.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <Card title={changePasswordCopy.title}>
            <Form form={form} layout="vertical" onFinish={onFinish} style={{ maxWidth: 400 }}>
                <Form.Item
                    name="currentPassword"
                    label={changePasswordCopy.currentPassword}
                    rules={[{ required: true, message: 'Aktuelles Passwort erforderlich' }]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="current-password" />
                </Form.Item>
                <Form.Item
                    name="newPassword"
                    label={changePasswordCopy.newPassword}
                    rules={[
                        { required: true, message: 'Neues Passwort erforderlich' },
                        { min: 8, message: 'Mindestens 8 Zeichen' },
                    ]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="new-password" />
                </Form.Item>
                <Form.Item
                    name="confirmPassword"
                    label={changePasswordCopy.confirmPassword}
                    dependencies={['newPassword']}
                    rules={[
                        { required: true, message: 'Bitte bestätigen Sie das neue Passwort' },
                        ({ getFieldValue }) => ({
                            validator(_, value) {
                                if (!value || getFieldValue('newPassword') === value) return Promise.resolve();
                                return Promise.reject(new Error(changePasswordCopy.confirmMismatch));
                            },
                        }),
                    ]}
                >
                    <Input.Password placeholder="••••••••" autoComplete="new-password" />
                </Form.Item>
                <Form.Item>
                    <Button type="primary" htmlType="submit" loading={loading} icon={<LockOutlined />}>
                        {changePasswordCopy.submit}
                    </Button>
                </Form.Item>
            </Form>
        </Card>
    );
}
