'use client';

import React, { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Select, Switch, message } from 'antd';
import { Product, TaxType } from '@/api/generated/model';

interface ProductFormProps {
    visible: boolean;
    initialValues?: Product | null;
    onCancel: () => void;
    onSubmit: (values: Product) => Promise<void>;
    loading?: boolean;
}

const { Option } = Select;
const { TextArea } = Input;

export default function ProductForm({
    visible,
    initialValues,
    onCancel,
    onSubmit,
    loading,
}: ProductFormProps) {
    const [form] = Form.useForm();

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                form.setFieldsValue({
                    ...initialValues,
                    isActive: initialValues.isActive ?? true, // Default active if undefined
                    taxType: initialValues.taxType || TaxType.NUMBER_20, // Default to 20%
                });
            } else {
                form.resetFields();
                form.setFieldsValue({
                    isActive: true,
                    taxType: TaxType.NUMBER_20,
                    taxRate: 20,
                    stockQuantity: 0,
                    minStockLevel: 5,
                    price: 0,
                    cost: 0
                });
            }
        }
    }, [visible, initialValues, form]);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();
            // Ensure numeric values are numbers
            const processedValues: Product = {
                ...values,
                price: Number(values.price),
                cost: Number(values.cost),
                stockQuantity: Number(values.stockQuantity),
                minStockLevel: Number(values.minStockLevel),
                taxRate: Number(values.taxRate),
            };
            await onSubmit(processedValues);
            form.resetFields();
        } catch (error: any) {
            console.error('Operation failed:', error);

            // Handle Backend Validation Errors
            if (error?.response?.data?.errors) {
                // Map Backend Validation Errors to AntD Form
                const validationErrors = error.response.data.errors;
                const formErrors = Object.keys(validationErrors).map(key => {
                    // Convert PascalCase (e.g. "Unit") to camelCase (e.g. "unit")
                    const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                    return {
                        name: camelKey,
                        errors: validationErrors[key]
                    };
                });
                form.setFields(formErrors);
            } else if (error?.response?.data?.title) {
                message.error(error.response.data.title);
            }
        }
    };

    const handleTaxTypeChange = (value: number) => {
        form.setFieldsValue({ taxRate: value });
    };

    return (
        <Modal
            title={initialValues ? 'Edit Product' : 'Create New Product'}
            open={visible}
            onOk={handleOk}
            onCancel={onCancel}
            confirmLoading={loading}
            width={600}
            destroyOnClose
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ isActive: true }}
            >
                <Form.Item
                    name="name"
                    label="Product Name"
                    rules={[{ required: true, message: 'Please enter product name' }]}
                >
                    <Input placeholder="E.g., Coca Cola 0.33L" />
                </Form.Item>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="barcode"
                        label="Barcode"
                    >
                        <Input placeholder="Scan or type barcode" />
                    </Form.Item>

                    <Form.Item
                        name="category"
                        label="Category"
                    >
                        <Input placeholder="E.g., Drinks" />
                    </Form.Item>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="price"
                        label="Price (€)"
                        rules={[{ required: true, message: 'Please enter price' }]}
                    >
                        <InputNumber
                            style={{ width: '100%' }}
                            min={0}
                            precision={2}
                            prefix="€"
                        />
                    </Form.Item>

                    <Form.Item
                        name="cost"
                        label="Cost (€)"
                    >
                        <InputNumber
                            style={{ width: '100%' }}
                            min={0}
                            precision={2}
                            prefix="€"
                        />
                    </Form.Item>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="taxType"
                        label="Tax Type"
                        rules={[{ required: true, message: 'Select tax type' }]}
                    >
                        <Select onChange={handleTaxTypeChange}>
                            <Option value={TaxType.NUMBER_10}>10% (Reduced)</Option>
                            <Option value={TaxType.NUMBER_13}>13% (Special)</Option>
                            <Option value={TaxType.NUMBER_20}>20% (Standard)</Option>
                        </Select>
                    </Form.Item>

                    {/* Hidden or ReadOnly Tax Rate linked to Tax Type */}
                    <Form.Item
                        name="taxRate"
                        label="Tax Rate (%)"
                        rules={[{ required: true }]}
                    >
                        <InputNumber style={{ width: '100%' }} readOnly />
                    </Form.Item>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="stockQuantity"
                        label="Stock Quantity"
                        rules={[{ required: true }]}
                    >
                        <InputNumber style={{ width: '100%' }} min={0} precision={0} />
                    </Form.Item>

                    <Form.Item
                        name="minStockLevel"
                        label="Min Stock Alert Level"
                    >
                        <InputNumber style={{ width: '100%' }} min={0} precision={0} />
                    </Form.Item>
                </div>

                <Form.Item
                    name="unit"
                    label="Unit"
                    rules={[{ required: true, message: 'Please enter unit (e.g. pcs, kg)' }]}
                >
                    <Input placeholder="pcs, kg, l" />
                </Form.Item>

                <Form.Item
                    name="description"
                    label="Description"
                >
                    <TextArea rows={3} />
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
