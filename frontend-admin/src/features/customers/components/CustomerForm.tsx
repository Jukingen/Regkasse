import React, { useEffect, useMemo, useState } from 'react';
import { Form, Input, Switch, Modal, Row, Col, Alert, Tag } from 'antd';
import { Customer } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { isSystemCustomer } from '@/features/customers/constants/walkInCustomer';
import { createValidationRules } from '@/lib/validation';
import { FormFieldWithTooltip, AutoSaveStatusIndicator } from '@/components/form';
import { FieldTooltip } from '@/components/FieldTooltip';
import {
    useAutoSave,
    clearAutoSaveDraft,
    readAutoSaveDraft,
    writeAutoSaveDraft,
} from '@/hooks/useAutoSave';

interface CustomerFormProps {
    visible: boolean;
    initialValues?: Customer | null;
    onCancel: () => void;
    onSubmit: (values: Customer) => void;
    loading: boolean;
    /** Admin assignment visibility only: count from benefit-summary. Same API as POS preview but distinct intent. Shown only when editing. */
    assignedBenefitCount?: number | null;
}

type CustomerFormDraft = Record<string, unknown>;

export default function CustomerForm({ visible, initialValues, onCancel, onSubmit, loading, assignedBenefitCount }: CustomerFormProps) {
    const { t } = useI18n();
    const { user } = useAuth();
    const superAdmin = isSuperAdmin(user?.role);
    const systemCustomer = Boolean(initialValues && isSystemCustomer(initialValues));
    const readOnly = systemCustomer && !superAdmin;
    const [form] = Form.useForm();
    const rules = useMemo(() => createValidationRules(t), [t]);
    const [draftValues, setDraftValues] = useState<CustomerFormDraft>({});

    const draftKey = initialValues?.id
        ? `fa:draft:customer:${initialValues.id}`
        : 'fa:draft:customer:new';

    const { saving, saved, error: autoSaveError } = useAutoSave(
        draftValues,
        async (data) => {
            writeAutoSaveDraft(draftKey, data);
        },
        700,
        { enabled: visible && !readOnly, skipInitial: true },
    );

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                form.setFieldsValue(initialValues);
                setDraftValues(initialValues as unknown as CustomerFormDraft);
            } else {
                form.resetFields();
                const defaults = {
                    isActive: true,
                    isVip: false,
                    loyaltyPoints: 0,
                };
                const draft = readAutoSaveDraft<CustomerFormDraft>(draftKey);
                if (draft && !draft.id) {
                    form.setFieldsValue({ ...defaults, ...draft });
                    setDraftValues({ ...defaults, ...draft });
                } else {
                    form.setFieldsValue(defaults);
                    setDraftValues(defaults);
                }
            }
        } else {
            setDraftValues({});
        }
    }, [visible, initialValues, form, draftKey]);

    const handleSubmit = () => {
        if (readOnly) return;
        form.validateFields().then((values) => {
            clearAutoSaveDraft(draftKey);
            onSubmit({
                ...initialValues,
                ...values,
            });
        });
    };

    return (
        <Modal
            title={
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 12 }}>
                    {initialValues ? t('customers.form.titleEdit') : t('customers.form.titleNew')}
                    {!readOnly && (
                        <AutoSaveStatusIndicator saving={saving} saved={saved} error={autoSaveError} />
                    )}
                </span>
            }
            open={visible}
            forceRender
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
            <Form
                form={form}
                layout="vertical"
                disabled={readOnly}
                onValuesChange={(_, all) => setDraftValues(all as CustomerFormDraft)}
            >
                <Row gutter={16}>
                    <Col span={12}>
                        <Form.Item
                            label={t('customers.form.name')}
                            name="name"
                            rules={[rules.required(t('customers.form.name'))]}
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
                            rules={[rules.email]}
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
                        <FormFieldWithTooltip
                            label={
                                <FieldTooltip title={t('customers.form.taxNumberTooltip')}>
                                    {t('customers.form.taxNumber')}
                                </FieldTooltip>
                            }
                            name="taxNumber"
                        >
                            <Input />
                        </FormFieldWithTooltip>
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
