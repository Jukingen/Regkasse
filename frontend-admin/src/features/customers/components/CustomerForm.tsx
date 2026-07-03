import React, { useEffect } from 'react';
import { Form, Input, Switch, Modal, Row, Col, Alert, Tag } from 'antd';
import { Customer } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { isSystemCustomer } from '@/features/customers/constants/walkInCustomer';

interface CustomerFormProps {
    visible: boolean;
    initialValues?: Customer | null;
    onCancel: () => void;
    onSubmit: (values: Customer) => void;
    loading: boolean;
    /** Admin assignment visibility only: count from benefit-summary. Same API as POS preview but distinct intent. Shown only when editing. */
    assignedBenefitCount?: number | null;
}

export default function CustomerForm({ visible, initialValues, onCancel, onSubmit, loading, assignedBenefitCount }: CustomerFormProps) {
    const { t } = useI18n();
    const { user } = useAuth();
    const superAdmin = isSuperAdmin(user?.role);
    const systemCustomer = Boolean(initialValues && isSystemCustomer(initialValues));
    const readOnly = systemCustomer && !superAdmin;
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
        if (readOnly) return;
        form.validateFields().then((values) => {
            onSubmit({
                ...initialValues,
                ...values,
            });
        });
    };

    return (
        <Modal
            title={initialValues ? t('customers.form.titleEdit') : t('customers.form.titleNew')}
            open={visible}
            onCancel={onCancel}
            onOk={readOnly ? undefined : handleSubmit}
            confirmLoading={loading}
            width={700}
            okText={t('common.buttons.save')}
            cancelText={t('common.buttons.cancel')}
            okButtonProps={{ style: readOnly ? { display: 'none' } : undefined }}
        >
            {readOnly && (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={
                        <span>
                            <Tag color="blue" style={{ marginInlineEnd: 8 }}>{t('customers.list.tagSystem')}</Tag>
                            <Tag color="default">{t('customers.list.tagProtected')}</Tag>
                        </span>
                    }
                    description={t('customers.list.systemCustomerProtected')}
                />
            )}
            <Form form={form} layout="vertical" disabled={readOnly}>
                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.name')}
                            name="name"
                            rules={[{ required: true, message: t('customers.form.nameRequired') }]}
                        >
                            <Input placeholder={t('customers.form.namePlaceholder')} />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.customerNumber')}
                            name="customerNumber"
                        >
                            <Input placeholder={t('customers.form.customerNumberPlaceholder')} />
                        </Form.Item>
                    </Col>
                </Row>

                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.email')}
                            name="email"
                            rules={[{ type: 'email' }]}
                        >
                            <Input placeholder={t('customers.form.emailPlaceholder')} />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.phone')}
                            name="phone"
                        >
                            <Input placeholder={t('customers.form.phonePlaceholder')} />
                        </Form.Item>
                    </Col>
                </Row>

                <Form.Item
                    label={t('customers.form.address')}
                    name="address"
                >
                    <Input.TextArea rows={2} />
                </Form.Item>

                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.taxNumber')}
                            name="taxNumber"
                        >
                            <Input />
                        </Form.Item>
                    </Col>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.birthDate')}
                            name="birthDate"
                        >
                            <Input type="date" />
                        </Form.Item>
                    </Col>
                </Row>

                <Form.Item
                    label={t('customers.form.notes')}
                    name="notes"
                >
                    <Input.TextArea rows={2} />
                </Form.Item>

                <Row gutter={16}>
                    <Col span={6}>
                        <Form.Item
                            name="isActive"
                            valuePropName="checked"
                            label={t('customers.form.active')}
                        >
                            <Switch />
                        </Form.Item>
                    </Col>
                    <Col span={6}>
                        <Form.Item
                            name="isVip"
                            valuePropName="checked"
                            label={t('customers.form.vip')}
                        >
                            <Switch />
                        </Form.Item>
                    </Col>
                </Row>
                {initialValues && assignedBenefitCount != null && (
                    <Row>
                        <Col span={24}>
                            <div style={{ fontSize: 12, color: '#666' }}>
                                {t('customers.form.assignedBenefitsHint', { count: assignedBenefitCount })}
                            </div>
                        </Col>
                    </Row>
                )}
            </Form>
        </Modal>
    );
}
