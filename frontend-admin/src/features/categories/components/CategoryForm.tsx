'use client';

import React, { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Switch } from 'antd';
import type { Category } from '@/api/generated/model';
import type { CreateCategoryFormValues, UpdateCategoryFormValues } from '../types';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { useI18n } from '@/i18n';

export type CategoryFormSubmitValues = CreateCategoryFormValues | UpdateCategoryFormValues;

interface CategoryFormProps {
    visible: boolean;
    initialValues?: Category | (Category & { vatRate?: number }) | null;
    onCancel: () => void;
    onSubmit: (values: CategoryFormSubmitValues) => Promise<void>;
    loading?: boolean;
}

export default function CategoryForm({
    visible,
    initialValues,
    onCancel,
    onSubmit,
    loading,
}: CategoryFormProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<CategoryFormSubmitValues>();

    useEffect(() => {
        if (visible) {
            const withVat = initialValues as (Category & { vatRate?: number }) | undefined;
            if (withVat) {
                form.setFieldsValue({
                    name: withVat.name,
                    vatRate: (withVat as { vatRate?: number }).vatRate ?? 20,
                    sortOrder: withVat.sortOrder ?? 0,
                    isActive: withVat.isActive ?? true,
                });
            } else {
                form.resetFields();
                form.setFieldsValue({
                    name: '',
                    vatRate: 20,
                    sortOrder: 0,
                    isActive: true,
                });
            }
        }
    }, [visible, initialValues, form]);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();
            await onSubmit({
                name: values.name!,
                vatRate: values.vatRate ?? 20,
                sortOrder: values.sortOrder ?? 0,
                isActive: values.isActive ?? true,
            });
            form.resetFields();
        } catch (error) {
            technicalConsole.error('[CategoryForm] form validation or submit failed', error);
        }
    };

    const vatMin = 0;
    const vatMax = 100;

    return (
        <Modal
            title={
                initialValues
                    ? t('common.categories.form.modalTitleEdit')
                    : t('common.categories.form.modalTitleCreate')
            }
            open={visible}
            onOk={handleOk}
            onCancel={onCancel}
            confirmLoading={loading}
            okText={t('common.buttons.save')}
            cancelText={t('common.buttons.cancel')}
            destroyOnHidden
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ vatRate: 20, sortOrder: 0, isActive: true }}
            >
                <Form.Item
                    name="name"
                    label={t('common.categories.form.nameLabel')}
                    rules={[{ required: true, message: t('common.categories.form.nameRequired') }]}
                >
                    <Input placeholder={t('common.categories.form.namePlaceholder')} />
                </Form.Item>

                <Form.Item
                    name="vatRate"
                    label={t('common.categories.form.vatLabel')}
                    rules={[
                        { required: true, message: t('common.categories.form.vatRequired') },
                        {
                            type: 'number',
                            min: vatMin,
                            max: vatMax,
                            message: t('common.categories.form.vatRange', { min: vatMin, max: vatMax }),
                        },
                    ]}
                >
                    <InputNumber
                        style={{ width: '100%' }}
                        min={vatMin}
                        max={vatMax}
                        precision={2}
                        addonAfter="%"
                        placeholder={t('common.categories.form.vatPlaceholder')}
                    />
                </Form.Item>

                <Form.Item name="sortOrder" label={t('common.categories.form.sortOrderLabel')}>
                    <InputNumber style={{ width: '100%' }} min={0} precision={0} />
                </Form.Item>

                <Form.Item
                    name="isActive"
                    label={t('common.categories.form.activeLabel')}
                    valuePropName="checked"
                >
                    <Switch />
                </Form.Item>
            </Form>
        </Modal>
    );
}
