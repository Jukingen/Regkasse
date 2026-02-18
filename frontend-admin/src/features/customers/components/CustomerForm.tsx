import React, { useEffect } from 'react';
import { Form, Input, Switch, Modal, Row, Col } from 'antd';
import { Customer } from '@/api/generated/model';

interface CustomerFormProps {
    visible: boolean;
    initialValues?: Customer | null;
    onCancel: () => void;
    onSubmit: (values: Customer) => void;
    loading: boolean;
}

export default function CustomerForm({ visible, initialValues, onCancel, onSubmit, loading }: CustomerFormProps) {
    const [form] = Form.useForm();

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                form.setFieldsValue(initialValues);
            } else {
                form.resetFields();
                form.setFieldsValue({
                    isActive: true,
                    isVip: false,
                    loyaltyPoints: 0,
                });
            }
        }
    }, [visible, initialValues, form]);

    const handleSubmit = () => {
        form.validateFields().then((values) => {
            onSubmit({
                ...initialValues,
                ...values,
            });
        });
    };

    return (
        <Modal
            title={initialValues ? 'Edit Customer' : 'New Customer'}
            open={visible}
            onCancel={onCancel}
            onOk={handleSubmit}
            confirmLoading={loading}
            width={700}
        >
            <Form form={form} layout="vertical">
                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label="Name"
                            name="name"
                            rules={[{ required: true, message: 'Required' }]}
                        >
                            <Input placeholder="John Doe" />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label="Customer Number"
                            name="customerNumber"
                        >
                            <Input placeholder="Auto-generated if empty" />
                        </Form.Item>
                    </Col>
                </Row>

                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label="Email"
                            name="email"
                            rules={[{ type: 'email' }]}
                        >
                            <Input placeholder="john@example.com" />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label="Phone"
                            name="phone"
                        >
                            <Input placeholder="+43 ..." />
                        </Form.Item>
                    </Col>
                </Row>

                <Form.Item
                    label="Address"
                    name="address"
                >
                    <Input.TextArea rows={2} />
                </Form.Item>

                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label="Tax Number"
                            name="taxNumber"
                        >
                            <Input />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label="Birth Date"
                            name="birthDate"
                        >
                            <Input type="date" />
                        </Form.Item>
                    </Col>
                </Row>

                <Form.Item
                    label="Notes"
                    name="notes"
                >
                    <Input.TextArea rows={2} />
                </Form.Item>

                <Row gutter={16}>
                    <Col span={6}>
                        <Form.Item
                            name="isActive"
                            valuePropName="checked"
                            label="Active"
                        >
                            <Switch />
                        </Form.Item>
                    </Col>
                    <Col span={6}>
                        <Form.Item
                            name="isVip"
                            valuePropName="checked"
                            label="VIP"
                        >
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
            </Form>
        </Modal>
    );
}
