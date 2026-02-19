'use client';

import React, { useEffect, useMemo } from 'react';
import { Form, Input, InputNumber, Modal, Select, Switch, message } from 'antd';
import { Product, TaxType } from '@/api/generated/model';
import DebounceSelect from '@/components/DebounceSelect';
import { getApiCategoriesSearch } from '@/api/generated/categories/categories';
import { useCategories } from '@/features/categories/hooks/useCategories';

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

    // Fetch default categories
    const { useList } = useCategories();
    const { data: categoryList } = useList();

    // Memoize default options
    const defaultCategoryOptions = useMemo(() => {
        return (categoryList || []).map((cat: any) => ({
            label: cat.Name || cat.name,
            value: cat.Name || cat.name,
        }));
    }, [categoryList]);

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                // Ensure we handle the case where initialValues.category is a string
                const catName = (initialValues.category as any)?.Name || (initialValues.category as any)?.name || initialValues.category;

                form.setFieldsValue({
                    ...initialValues,
                    isActive: initialValues.isActive ?? true,
                    taxType: initialValues.taxType || TaxType.NUMBER_20,
                    // Ensure hidden fields are populated so they are returned safely if we need them, 
                    // though we will likely override them with defaults if they are missing/null
                    unit: initialValues.unit || 'pcs',
                    stockQuantity: initialValues.stockQuantity ?? 0,
                    minStockLevel: initialValues.minStockLevel ?? 0,
                    // If DebounceSelect is in labelInValue mode, we need { label, value }
                    // But if we pass string, AntD Select might handle it if options are available.
                    // Safer to pass the object if we can, or just the value if not labelInValue.
                    // DebounceSelect uses labelInValue={true} in the component definition.
                    category: catName ? { label: catName, value: catName } : undefined
                });
            } else {
                form.resetFields();
                form.setFieldsValue({
                    isActive: true,
                    taxType: TaxType.NUMBER_20,
                    taxRate: 20,
                    price: 0,
                    cost: 0,
                    // Hidden defaults
                    unit: 'pcs',
                    stockQuantity: 0,
                    minStockLevel: 0,
                });
            }
        }
    }, [visible, initialValues, form]);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();

            // Extract category value (it might be an object from DebounceSelect or a string if not changed? 
            // Actually DebounceSelect with labelInValue returns object { label, value }. 
            // If it was initialValue string, we need to handle that.
            // Wait, I set initialValue as { label, value } in useEffect. So it should be consistent.

            // Extract category value if it's an object (labelInValue)
            const categoryValue = values.category?.value || values.category;

            // Prepare payload with hidden defaults + visible values
            const processedValues: Product = {
                ...values,
                price: Number(values.price),
                cost: Number(values.cost),
                taxRate: Number(values.taxRate),
                // Ensure defaults for hidden fields
                stockQuantity: Number(values.stockQuantity ?? 0),
                minStockLevel: Number(values.minStockLevel ?? 0),
                unit: values.unit || 'pcs',
                category: categoryValue,
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

                // If there's an error on a hidden field (shouldn't happen with defaults, but just in case),
                // we might want to know.
            } else if (error?.response?.data?.title) {
                message.error(error.response.data.title);
            }
        }
    };

    const handleTaxTypeChange = (value: number) => {
        form.setFieldsValue({ taxRate: value });
    };

    // Category Search Fetcher
    const fetchCategories = async (search: string) => {
        if (!search) return defaultCategoryOptions;
        try {
            const data = await getApiCategoriesSearch({ query: search });
            return data.map((cat: any) => ({
                label: cat.Name || cat.name,
                value: cat.Name || cat.name,
            }));
        } catch (error) {
            console.error('Failed to fetch categories', error);
            return [];
        }
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
                {/* Hidden Fields to hold state */}
                <Form.Item name="unit" hidden><Input /></Form.Item>
                <Form.Item name="stockQuantity" hidden><InputNumber /></Form.Item>
                <Form.Item name="minStockLevel" hidden><InputNumber /></Form.Item>

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
                        <DebounceSelect
                            placeholder="Select or Search Category"
                            fetchOptions={fetchCategories}
                            defaultOptions={defaultCategoryOptions}
                            style={{ width: '100%' }}
                            allowClear
                        />
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
