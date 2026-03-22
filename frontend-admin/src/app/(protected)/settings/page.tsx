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

const ATU_REGEX = /^ATU\d{8}$/;

/** User-facing load error text; avoids dumping unknown error shapes raw into the UI */
function getSettingsLoadErrorDescription(err: unknown): string {
    if (err instanceof Error && err.message.trim()) return err.message.trim();
    const normalized = (err as { normalized?: { message?: string } })?.normalized;
    if (normalized?.message?.trim()) return normalized.message.trim();
    const msg = (err as { message?: string })?.message;
    if (typeof msg === 'string' && msg.trim()) return msg.trim();
    return 'Unable to load company settings. Check your connection and try again.';
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
            message.success('Settings saved successfully');
        } catch (err) {
            message.error('Failed to save settings');
        }
    };

    const headerBreadcrumbs = [
        { title: 'Dashboard', href: '/dashboard' },
        { title: 'Settings' },
    ] as const;

    if (isLoading) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title="Company Settings" breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <div style={{ textAlign: 'center', padding: '48px 24px' }}>
                        <Spin size="large" />
                        <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
                            Loading company settings…
                        </Typography.Paragraph>
                    </div>
                </Card>
            </SpaceWrapper>
        );
    }

    if (isError) {
        return (
            <SpaceWrapper>
                <AdminPageHeader title="Company Settings" breadcrumbs={[...headerBreadcrumbs]} />
                <Alert
                    type="error"
                    message="Failed to load company settings"
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
                <AdminPageHeader title="Company Settings" breadcrumbs={[...headerBreadcrumbs]} />
                <Card>
                    <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description="No company settings were returned."
                    >
                        <Button type="primary" onClick={() => refetch()} loading={isFetching}>
                            Reload
                        </Button>
                    </Empty>
                </Card>
            </SpaceWrapper>
        );
    }

    return (
        <SpaceWrapper>
            <AdminPageHeader
                title="Company Settings"
                breadcrumbs={[...headerBreadcrumbs]}
                actions={
                    <Button
                        type="primary"
                        icon={<SaveOutlined />}
                        onClick={() => form.submit()}
                        loading={updateMutation.isPending}
                    >
                        Save Changes
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
                            label: 'Localization',
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
        <Card title="Company Information">
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Company Name"
                        name="companyName"
                        rules={[{ required: true, message: 'Company name is required' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label="Company Address"
                        name="companyAddress"
                        rules={[{ required: true, message: 'Company address is required' }]}
                    >
                        <Input.TextArea rows={3} />
                    </Form.Item>
                    <Form.Item
                        label="Company Tax Number"
                        name="companyTaxNumber"
                        rules={[
                            { required: true, message: 'Tax number is required' },
                            { pattern: ATU_REGEX, message: 'ATU format: ATU + 8 digits' },
                        ]}
                    >
                        <Input placeholder="ATU12345678" />
                    </Form.Item>
                    <Form.Item
                        label="VAT Number"
                        name="companyVatNumber"
                        rules={[{ pattern: ATU_REGEX, message: 'ATU format: ATU + 8 digits' }]}
                    >
                        <Input placeholder="ATU12345678" />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Contact Person" name="contactPerson">
                        <Input />
                    </Form.Item>
                    <Form.Item
                        label="Contact Email"
                        name="contactEmail"
                        rules={[{ type: 'email', message: 'Invalid email format' }]}
                    >
                        <Input />
                    </Form.Item>
                    <Form.Item label="Contact Phone" name="contactPhone">
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
                    <Form.Item label="Bank Name" name="bankName">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Bank Account (IBAN)" name="bankAccountNumber">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="BIC / Swift" name="bankSwiftCode">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
        </Card>
    );
}

function LocalizationTab() {
    return (
        <Card title="Localization & Formatting">
            <Row gutter={24}>
                <Col span={8}>
                    <Form.Item
                        label="Language"
                        name="defaultLanguage"
                        rules={[{ required: true, message: 'Language is required' }]}
                    >
                        <Input placeholder="de-DE" />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label="Currency"
                        name="defaultCurrency"
                        rules={[{ required: true, message: 'Currency is required' }]}
                    >
                        <Input placeholder="EUR" />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item
                        label="Time Zone"
                        name="defaultTimeZone"
                        rules={[{ required: true, message: 'Time zone is required' }]}
                    >
                        <Input placeholder="Europe/Vienna" />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Date Format"
                        name="defaultDateFormat"
                        rules={[{ required: true }]}
                    >
                        <Input placeholder="dd.MM.yyyy" />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label="Time Format"
                        name="defaultTimeFormat"
                        rules={[{ required: true }]}
                    >
                        <Input placeholder="HH:mm:ss" />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item
                        label="Receipt Numbering"
                        name="receiptNumbering"
                        rules={[{ required: true }]}
                    >
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item
                        label="Invoice Numbering"
                        name="invoiceNumbering"
                        rules={[{ required: true }]}
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
            <Card title="Credentials & Connectivity">
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnabled" valuePropName="checked" label="Enable FinanzOnline Integration">
                            <Switch />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="API URL"
                            name="finanzOnlineApiUrl"
                            rules={[
                                { max: 500, message: 'Max 500 characters' },
                                { type: 'url', message: 'Provide a valid URL' },
                            ]}
                        >
                            <Input placeholder="https://finanzonline.example.at/api" />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Participant ID (Teilnehmer-ID)"
                            name="finanzOnlineParticipantId"
                            rules={[{ max: 100, message: 'Max 100 characters' }]}
                            extra="Teilnehmer-ID or combined Teilnehmer-ID/Benutzer-ID if required by provider"
                        >
                            <Input placeholder="Teilnehmer-ID" />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="PIN"
                            name="finanzOnlinePin"
                            rules={[{ max: 100, message: 'Max 100 characters' }]}
                            extra="Aus Sicherheitsgründen wird ein bestehender PIN nie angezeigt. Feld leer lassen, um den aktuellen PIN beizubehalten."
                        >
                            <Input.Password placeholder="PIN" />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title="Submission Behavior">
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Session Timeout (min)"
                            name="finanzOnlineSubmitInterval"
                            rules={[{ type: 'number', min: 1, max: 1440, message: 'Between 1 and 1440' }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={1} max={1440} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineAutoSubmit" valuePropName="checked" label="Automatic Submission">
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title="Validation & Retry Policy">
                <Row gutter={24}>
                    <Col xs={24} md={12}>
                        <Form.Item
                            label="Retry Attempts"
                            name="finanzOnlineRetryAttempts"
                            rules={[{ type: 'number', min: 0, max: 20, message: 'Between 0 and 20' }]}
                        >
                            <InputNumber style={{ width: '100%' }} min={0} max={20} />
                        </Form.Item>
                    </Col>
                    <Col xs={24} md={12}>
                        <Form.Item name="finanzOnlineEnableValidation" valuePropName="checked" label="Enable Payload Validation">
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Card>

            <Card title="Operational State / Health">
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    Laufzeitstatus aus dem Backend. Diese Felder sind read-only und werden nicht über Save Changes geschrieben.
                </Typography.Paragraph>
                <Descriptions size="small" bordered column={1}>
                    <Descriptions.Item label="Last FinanzOnline Sync">
                        <Form.Item noStyle shouldUpdate>
                            {({ getFieldValue }) => {
                                const v = getFieldValue('lastFinanzOnlineSync');
                                return v ? String(v) : '—';
                            }}
                        </Form.Item>
                    </Descriptions.Item>
                    <Descriptions.Item label="Pending Invoices">
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
        <Card title="TSE (Technical Security Element)">
            <Form.Item name="tseAutoConnect" valuePropName="checked" label="Auto Connect on Startup">
                <Switch />
            </Form.Item>

            <Form.Item
                label="Default TSE Device ID"
                name="defaultTseDeviceId"
                rules={[{ max: 100, message: 'Max 100 characters' }]}
            >
                <Input />
            </Form.Item>

            <Form.Item
                label="Connection Timeout (ms)"
                name="tseConnectionTimeout"
                rules={[{ type: 'number', min: 5, max: 120000, message: 'Between 5 and 120000' }]}
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
