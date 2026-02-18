'use client';

import React, { useEffect } from 'react';
import { Form, Input, Button, Card, Tabs, message, Row, Col, InputNumber, Switch, Divider, Spin } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
    useGetApiCompanySettings,
    usePutApiCompanySettings
} from '@/api/generated/company-settings/company-settings';
import { UpdateCompanySettingsRequest } from '@/api/generated/model';

export default function SettingsPage() {
    const { data: settings, isLoading, error } = useGetApiCompanySettings();
    const updateMutation = usePutApiCompanySettings();
    const [form] = Form.useForm();

    useEffect(() => {
        if (settings) {
            form.setFieldsValue(settings);
        }
    }, [settings, form]);

    const handleSave = async (values: UpdateCompanySettingsRequest) => {
        try {
            await updateMutation.mutateAsync({ data: values });
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

            <Form
                form={form}
                layout="vertical"
                onFinish={handleSave}
                initialValues={settings}
            >
                <Tabs defaultActiveKey="1" items={[
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
                ]} />
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
                    <Form.Item label="Company Name" name="companyName" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item label="Company Address" name="companyAddress" rules={[{ required: true }]}>
                        <Input.TextArea rows={3} />
                    </Form.Item>
                    <Form.Item label="Company Tax Number" name="companyTaxNumber" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item label="VAT Number" name="companyVatNumber">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Contact Person" name="contactPerson">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Contact Email" name="contactEmail" rules={[{ type: 'email' }]}>
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
                    <Form.Item label="Language" name="language">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item label="Currency" name="currency">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={8}>
                    <Form.Item label="Time Zone" name="timeZone">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label="Date Format" name="dateFormat">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Time Format" name="timeFormat">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label="Receipt Numbering" name="receiptNumbering">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Invoice Numbering" name="invoiceNumbering">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>
        </Card>
    )
}

function FinanzOnlineTab() {
    return (
        <Card title="FinanzOnline Settings">
            <Form.Item name="finanzOnlineEnabled" valuePropName="checked" label="Enable FinanzOnline Integration">
                <Switch />
            </Form.Item>

            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label="Participant ID (Teilnehmer-ID)" name="finanzOnlineUsername">
                        <Input />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="User ID (Benutzer-ID)" name="finanzOnlineUsername">
                        <Input />
                    </Form.Item>
                </Col>
            </Row>

            <Row gutter={24}>
                <Col span={12}>
                    <Form.Item label="PIN" name="finanzOnlinePassword">
                        <Input.Password />
                    </Form.Item>
                </Col>
                <Col span={12}>
                    <Form.Item label="Session Timeout (min)" name="finanzOnlineSubmitInterval">
                        <InputNumber style={{ width: '100%' }} />
                    </Form.Item>
                </Col>
            </Row>

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

            <Form.Item label="Default TSE Device ID" name="defaultTseDeviceId">
                <Input />
            </Form.Item>

            <Form.Item label="Connection Timeout (ms)" name="tseConnectionTimeout">
                <InputNumber style={{ width: '100%' }} />
            </Form.Item>
        </Card>
    );
}
