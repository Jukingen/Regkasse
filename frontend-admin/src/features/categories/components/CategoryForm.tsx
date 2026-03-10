'use client';

import React, { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Switch } from 'antd';
import type { Category } from '@/api/generated/model';
import type { CreateCategoryFormValues, UpdateCategoryFormValues } from '../types';

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
            console.error('Validation failed:', error);
        }
    };

    return (
        <Modal
            title={initialValues ? 'Edit Category' : 'Create New Category'}
            open={visible}
            onOk={handleOk}
            onCancel={onCancel}
            confirmLoading={loading}
            destroyOnHidden
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ vatRate: 20, sortOrder: 0, isActive: true }}
            >
                <Form.Item
                    name="name"
                    label="Category Name"
                    rules={[{ required: true, message: 'Please enter category name' }]}
                >
                    <Input placeholder="E.g. Speisen, Getränke" />
                </Form.Item>

                <Form.Item
                    name="vatRate"
                    label="VAT Rate (%)"
                    rules={[
                        { required: true, message: 'Please enter VAT rate' },
                        { type: 'number', min: 0, max: 100, message: 'VAT rate must be 0–100' },
                    ]}
                >
                    <InputNumber
                        style={{ width: '100%' }}
                        min={0}
                        max={100}
                        precision={2}
                        addonAfter="%"
                        placeholder="10 or 20"
                    />
                </Form.Item>

                <Form.Item name="sortOrder" label="Sort Order">
                    <InputNumber style={{ width: '100%' }} min={0} precision={0} />
                </Form.Item>

                <Form.Item
                    name="isActive"
                    label="Active"
                    valuePropName="checked"
                >
                    <Switch />
                </Form.Item>
            </Form>
        </Modal>
    );
}
