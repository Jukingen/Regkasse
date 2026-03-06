'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Form, Input, InputNumber, Modal, Select, Switch, message } from 'antd';
import { Product } from '@/api/generated/model';
import { useCategories } from '@/features/categories/hooks/useCategories';
import { getModifierGroups, getProductModifierGroups, type ModifierGroupDto } from '@/lib/api/modifierGroups';
import { TAX_TYPE_ENUM } from '@/features/products/utils/productMapper';
import ExtraZutatenSection from './ExtraZutatenSection';

/** Dropdown seçenekleri: backend enum id (1,2,3) ve kullanıcı dostu etiket. */
const TAX_TYPE_OPTIONS = [
    { value: TAX_TYPE_ENUM.Standard, label: '20% (Standard)' },
    { value: TAX_TYPE_ENUM.Reduced, label: '10% (Reduced)' },
    { value: TAX_TYPE_ENUM.Special, label: '13% (Special)' },
] as const;

export type ProductFormSubmitValues = Product & { modifierGroupIds?: string[]; categoryId?: string };

interface ProductFormProps {
    visible: boolean;
    initialValues?: Product | null;
    onCancel: () => void;
    onSubmit: (values: ProductFormSubmitValues) => Promise<void>;
    loading?: boolean;
}

const { TextArea } = Input;

export default function ProductForm({
    visible,
    initialValues,
    onCancel,
    onSubmit,
    loading,
}: ProductFormProps) {
    const [form] = Form.useForm();
    const [modifierGroups, setModifierGroups] = useState<ModifierGroupDto[]>([]);
    const [selectedModifierGroupIds, setSelectedModifierGroupIds] = useState<string[]>([]);
    const [modifierGroupsLoading, setModifierGroupsLoading] = useState(false);

    // Extra Zutaten: Tüm grupları ve (edit modda) ürüne atanmış grupları yükle
    useEffect(() => {
        if (!visible) return;
        let cancelled = false;
        setModifierGroupsLoading(true);
        (async () => {
            try {
                const [allGroups, assignedGroups] = await Promise.all([
                    getModifierGroups(),
                    initialValues?.id ? getProductModifierGroups(initialValues.id) : Promise.resolve([]),
                ]);
                if (cancelled) return;
                setModifierGroups(allGroups);
                setSelectedModifierGroupIds(assignedGroups.map((g) => g.id));
            } catch (e) {
                if (!cancelled) {
                    message.error('Add-on-Gruppen konnten nicht geladen werden.');
                    setModifierGroups([]);
                    setSelectedModifierGroupIds([]);
                }
            } finally {
                if (!cancelled) setModifierGroupsLoading(false);
            }
        })();
        return () => { cancelled = true; };
    }, [visible, initialValues?.id]);

    // Kategori listesi: /api/admin/categories (useCategories → src/api/admin/categories)
    const { useList } = useCategories();
    const { data: categoryList } = useList();

    const categoryOptions = useMemo(() => {
        const list = categoryList ?? [];
        return list.map((cat: { id?: string; name?: string }) => ({
            label: cat.name ?? (cat as any).Name ?? '',
            value: cat.id ?? (cat as any).Id ?? '',
        })).filter((o: { value: string }) => o.value);
    }, [categoryList]);

    useEffect(() => {
        if (visible) {
            if (initialValues) {
                const product = initialValues as Product & { categoryId?: string };
                const categoryId = product.categoryId ?? (categoryOptions.find(
                    (o: { label: string }) => o.label === (product.category ?? '')
                ) as { value: string } | undefined)?.value;

                const taxType = Number((initialValues as any).taxType ?? TAX_TYPE_ENUM.Standard);
                form.setFieldsValue({
                    ...initialValues,
                    isActive: initialValues.isActive ?? true,
                    taxType: [TAX_TYPE_ENUM.Standard, TAX_TYPE_ENUM.Reduced, TAX_TYPE_ENUM.Special].includes(taxType) ? taxType : TAX_TYPE_ENUM.Standard,
                    unit: initialValues.unit || 'pcs',
                    stockQuantity: initialValues.stockQuantity ?? 0,
                    minStockLevel: initialValues.minStockLevel ?? 0,
                    categoryId: categoryId || undefined,
                });
            } else {
                form.resetFields();
                form.setFieldsValue({
                    isActive: true,
                    taxType: TAX_TYPE_ENUM.Standard,
                    price: 0,
                    cost: 0,
                    unit: 'pcs',
                    stockQuantity: 0,
                    minStockLevel: 0,
                });
            }
        }
    }, [visible, initialValues, form, categoryOptions]);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();

            // Kategori: dropdown value = categoryId (GUID); backend ayrıca [Required] Category (ad) bekliyor
            const categoryId = values.categoryId as string | undefined;
            if (!categoryId?.trim()) {
                message.error('Please select a category');
                return;
            }
            const categoryName =
                categoryOptions.find((o: { value: string }) => o.value === categoryId)?.label ??
                (initialValues as any)?.category ??
                '';

            const processedValues: ProductFormSubmitValues = {
                ...values,
                price: Number(values.price),
                cost: Number(values.cost),
                taxType: Number(values.taxType) as any,
                stockQuantity: Number(values.stockQuantity ?? 0),
                minStockLevel: Number(values.minStockLevel ?? 0),
                unit: values.unit || 'pcs',
                categoryId,
                category: categoryName,
                modifierGroupIds: selectedModifierGroupIds,
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
                        name="categoryId"
                        label="Category"
                        rules={[{ required: true, message: 'Please select a category' }]}
                    >
                        <Select
                            placeholder="Select category"
                            options={categoryOptions}
                            loading={!categoryList}
                            allowClear={false}
                            showSearch
                            optionFilterProp="label"
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

                <Form.Item
                    name="taxType"
                    label="Tax Type"
                    rules={[{ required: true, message: 'Select tax type' }]}
                >
                    <Select
                        options={TAX_TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                        placeholder="Select tax type"
                    />
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

                <Form.Item label="Vorgeschlagene Add-on-Gruppen" style={{ marginBottom: 0 }}>
                    <ExtraZutatenSection
                        groups={modifierGroups}
                        selectedGroupIds={selectedModifierGroupIds}
                        onChange={setSelectedModifierGroupIds}
                        loading={modifierGroupsLoading}
                    />
                </Form.Item>
            </Form>
        </Modal>
    );
}
