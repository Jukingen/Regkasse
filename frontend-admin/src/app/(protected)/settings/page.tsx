'use client';

import React from 'react';
import { Card, Typography, Tabs, Form, Input, Button, Switch, Space, Divider, Select } from 'antd';
import { SettingOutlined, BankOutlined, GlobalOutlined, BellOutlined } from '@ant-design/icons';

const { Title, Paragraph } = Typography;

export default function SettingsPage() {
    const [form] = Form.useForm();

    const tabs = [
        {
            key: 'general',
            label: <span><SettingOutlined /> General</span>,
            children: (
                <Form form={form} layout="vertical" initialValues={{ siteName: 'Regkasse Admin', language: 'tr' }}>
                    <Title level={4}>Application Settings</Title>
                    <Form.Item label="Admin Panel Name" name="siteName">
                        <Input />
                    </Form.Item>
                    <Form.Item label="Default Language" name="language">
                        <Select>
                            <Select.Option value="tr">Turkish</Select.Option>
                            <Select.Option value="de">German</Select.Option>
                            <Select.Option value="en">English</Select.Option>
                        </Select>
                    </Form.Item>
                    <Form.Item label="Enable Debug Mode" name="debug" valuePropName="checked">
                        <Switch />
                    </Form.Item>
                    <Button type="primary">Save Changes</Button>
                </Form>
            )
        },
        {
            key: 'company',
            label: <span><BankOutlined /> Company</span>,
            children: (
                <Form layout="vertical">
                    <Title level={4}>Company Details</Title>
                    <Form.Item label="Company Name">
                        <Input placeholder="Regkasse GmbH" />
                    </Form.Item>
                    <Form.Item label="Tax ID (UID)">
                        <Input placeholder="ATU12345678" />
                    </Form.Item>
                    <Form.Item label="Address">
                        <Input.TextArea rows={3} />
                    </Form.Item>
                    <Button type="primary">Update Company</Button>
                </Form>
            )
        },
        {
            key: 'localization',
            label: <span><GlobalOutlined /> Localization</span>,
            children: (
                <Space direction="vertical" style={{ width: '100%' }}>
                    <Title level={4}>Currency & Regions</Title>
                    <Paragraph>Manage decimal separators, currency symbols, and date formats.</Paragraph>
                    <Divider />
                    <Form layout="inline">
                        <Form.Item label="Currency Symbol">
                            <Input style={{ width: 80 }} defaultValue="€" />
                        </Form.Item>
                        <Form.Item label="Date Format">
                            <Select style={{ width: 150 }} defaultValue="DD.MM.YYYY">
                                <Select.Option value="DD.MM.YYYY">DD.MM.YYYY</Select.Option>
                                <Select.Option value="YYYY-MM-DD">YYYY-MM-DD</Select.Option>
                            </Select>
                        </Form.Item>
                    </Form>
                </Space>
            )
        }
    ];

    return (
        <Card>
            <Title level={3}>Settings</Title>
            <Tabs defaultActiveKey="general" items={tabs} />
        </Card>
    );
}
