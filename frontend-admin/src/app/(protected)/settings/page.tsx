'use client';

import React, { useEffect } from 'react';
import { Form, Input, Button, Card, Tabs, message, Row, Col, InputNumber, Switch, Divider, Spin } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
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

const ATU_REGEX = /^ATU\d{8}$/;

export default function SettingsPage() {
    const { data: settings, isLoading, error } = useGetApiCompanySettings();
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

    if (isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 50 }}>
                <Spin size="large" />
            </div>
        );
    }

    if (error) {
        return <div>Error loading settings</div>;
    }

    return (
        <SpaceWrapper>
            <AdminPageHeader
                title="Company Settings"
                breadcrumbs={[{ title: 'Dashboard', href: '/' }, { title: 'Settings' }]}
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
        <Card title="FinanzOnline Settings">
            <Form.Item name="finanzOnlineEnabled" valuePropName="checked" label="Enable FinanzOnline Integration">
                <Switch />
            </Form.Item>

            <Form.Item
                label="Participant ID (Teilnehmer-ID)"
                name="finanzOnlineParticipantId"
                rules={[{ max: 100, message: 'Max 100 characters' }]}
                extra="Teilnehmer-ID or combined Teilnehmer-ID/Benutzer-ID if required by provider"
            >
                <Input placeholder="Teilnehmer-ID" />
            </Form.Item>

            <Form.Item
                label="PIN"
                name="finanzOnlinePin"
                rules={[{ max: 100, message: 'Max 100 characters' }]}
            >
                <Input.Password placeholder="PIN" />
            </Form.Item>

            <Form.Item
                label="Session Timeout (min)"
                name="finanzOnlineSubmitInterval"
                rules={[{ type: 'number', min: 1, max: 1440, message: 'Between 1 and 1440' }]}
            >
                <InputNumber style={{ width: '100%' }} min={1} max={1440} />
            </Form.Item>

            <Form.Item name="finanzOnlineAutoSubmit" valuePropName="checked" label="Automatic Submission">
                <Switch />
            </Form.Item>
        </Card>
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
