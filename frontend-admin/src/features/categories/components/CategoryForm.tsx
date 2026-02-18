'use client';

import React, { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Switch } from 'antd';
import { CreateCategoryRequest, UpdateCategoryRequest, Category } from '@/api/generated/model';

interface CategoryFormProps {
    visible: boolean;
    initialValues?: Category | null;
    onCancel: () => void;
    onSubmit: (values: CreateCategoryRequest | UpdateCategoryRequest) => Promise<void>;
    loading?: boolean;
}

const { TextArea } = Input;

export default function CategoryForm({
    visible,
    initialValues,
    onCancel,
    onSubmit,
    loading,
}: CategoryFormProps) {
    const [form] = Form.useForm();

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                form.setFieldsValue({
                    ...initialValues,
                    isActive: initialValues.isActive ?? true,
                });
            } else {
                form.resetFields();
                form.setFieldsValue({
                    isActive: true,
                    sortOrder: 0,
                    color: '#ffffff',
                });
            }
        }
    }, [visible, initialValues, form]);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();
            await onSubmit(values);
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
            destroyOnClose
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ isActive: true }}
            >
                <Form.Item
                    name="name"
                    label="Category Name"
                    rules={[{ required: true, message: 'Please enter category name' }]}
                >
                    <Input placeholder="E.g., Drinks" />
                </Form.Item>

                <Form.Item
                    name="description"
                    label="Description"
                >
                    <TextArea rows={2} />
                </Form.Item>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="sortOrder"
                        label="Sort Order"
                    >
                        <InputNumber style={{ width: '100%' }} min={0} precision={0} />
                    </Form.Item>

                    <Form.Item
                        name="color"
                        label="Color (Hex)"
                        rules={[
                            { pattern: /^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$/, message: 'Please enter a valid hex color' }
                        ]}
                    >
                        <Input placeholder="#ffffff" />
                    </Form.Item>
                </div>

                <Form.Item
                    name="icon"
                    label="Icon (Optional)"
                >
                    <Input placeholder="Icon Name or URL" />
                </Form.Item>

                {/* Categories usually don't have isActive property in CreateCategoryRequest, but Category model has it.
                   The generated CreateCategoryRequest doesn't include isActive. Let's verify.
                   Yes, CreateCategoryRequest: color, description, icon, name, sortOrder.
                   Category: isActive.
                   So we might not be able to set isActive on creation via POST, only PUT perhaps?
                   Actually UpdateCategoryRequest also lacks isActive.
                   Let's check the swagger again. Step 389/390 confirm no isActive in Request DTOs.
                   So I will remove isActive form item if it's not editable via API.
                   Wait, I'll keep it renderable but maybe disabled if it's readonly?
                   Or checking if the backend automatically handles it.
                   I will remove it for now to be safe.
                */}
            </Form>
        </Modal>
    );
}
