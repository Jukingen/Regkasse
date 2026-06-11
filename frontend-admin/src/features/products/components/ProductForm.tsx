'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useEffect, useMemo, useState } from 'react';
import { Modal, Button, Collapse, Form, Input, InputNumber, Select, Switch, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { Product } from '@/api/generated/model';
import { useCategories } from '@/features/categories/hooks/useCategories';
import { getModifierGroups, getProductModifierGroups, type ModifierGroupDto } from '@/lib/api/modifierGroups';
import { TAX_TYPE_ENUM } from '@/features/products/utils/productMapper';
import ExtraZutatenSection from './ExtraZutatenSection';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { useI18n } from '@/i18n';
import { uploadAdminProductImage, MAX_PRODUCT_IMAGE_BYTES } from '@/api/admin/products';

export type ProductFormSubmitValues = Product & { modifierGroupIds?: string[]; categoryId?: string };

interface ProductFormProps {
    visible: boolean;
    initialValues?: Product | null;
    onCancel: () => void;
    onSubmit: (values: ProductFormSubmitValues) => Promise<void>;
    loading?: boolean;
}

const { TextArea } = Input;

export default function ProductForm(props: ProductFormProps) {
    if (!props.visible) {
        return null;
    }
    return <ProductFormContent {...props} />;
}

function ProductFormContent({
    visible,
    initialValues,
    onCancel,
    onSubmit,
    loading,
}: ProductFormProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [form] = Form.useForm();
    const [modifierGroups, setModifierGroups] = useState<ModifierGroupDto[]>([]);
    const [selectedModifierGroupIds, setSelectedModifierGroupIds] = useState<string[]>([]);
    const [modifierGroupsLoading, setModifierGroupsLoading] = useState(false);
    const [imageUploading, setImageUploading] = useState(false);
    const [imageUploadPercent, setImageUploadPercent] = useState<number | null>(null);

    // Load all modifier groups and (in edit mode) groups assigned to this product
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
                setSelectedModifierGroupIds(
                    assignedGroups.map((g) => String((g as { id?: string; Id?: string }).id ?? (g as { id?: string; Id?: string }).Id ?? '')).filter(Boolean)
                );
            } catch (e) {
                if (!cancelled) {
                    message.error(t('products.messages.modifierGroupsLoadFailed'));
                    setModifierGroups([]);
                    setSelectedModifierGroupIds([]);
                }
            } finally {
                if (!cancelled) setModifierGroupsLoading(false);
            }
        })();
        return () => { cancelled = true; };
    }, [visible, initialValues?.id, t]);

    const taxTypeOptions = useMemo(
        () => [
            { value: TAX_TYPE_ENUM.Standard, label: t('products.form.taxStandard') },
            { value: TAX_TYPE_ENUM.Reduced, label: t('products.form.taxReduced') },
            { value: TAX_TYPE_ENUM.Special, label: t('products.form.taxSpecial') },
        ],
        [t],
    );

    const productImageUrlRules = useMemo(
        () => [
            {
                validator: (_: unknown, value: unknown) => {
                    if (value === undefined || value === null) return Promise.resolve();
                    const s = String(value).trim();
                    if (s === '') return Promise.resolve();
                    if (s.length > 500) {
                        return Promise.reject(new Error(t('products.form.imageUrlTooLong')));
                    }
                    try {
                        const u = new URL(s);
                        if (u.protocol !== 'http:' && u.protocol !== 'https:') {
                            return Promise.reject(new Error(t('products.form.imageUrlInvalid')));
                        }
                        return Promise.resolve();
                    } catch {
                        return Promise.reject(new Error(t('products.form.imageUrlInvalid')));
                    }
                },
            },
        ],
        [t],
    );

    // Category list: /api/admin/categories (useCategories → src/api/admin/categories)
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

                const allowedTax = [TAX_TYPE_ENUM.Standard, TAX_TYPE_ENUM.Reduced, TAX_TYPE_ENUM.Special] as const;
                const rawTax = Number((initialValues as any).taxType ?? TAX_TYPE_ENUM.Standard);
                const taxTypeNorm = (allowedTax as readonly number[]).includes(rawTax)
                    ? (rawTax as (typeof allowedTax)[number])
                    : TAX_TYPE_ENUM.Standard;
                const iv = initialValues as Product & {
                    nameDe?: string;
                    nameEn?: string;
                    nameTr?: string;
                    descriptionDe?: string;
                    descriptionEn?: string;
                    descriptionTr?: string;
                };
                form.setFieldsValue({
                    ...initialValues,
                    nameDe: iv.nameDe ?? iv.name,
                    nameEn: iv.nameEn ?? '',
                    nameTr: iv.nameTr ?? '',
                    descriptionDe: iv.descriptionDe ?? iv.description ?? '',
                    descriptionEn: iv.descriptionEn ?? '',
                    descriptionTr: iv.descriptionTr ?? '',
                    isActive: initialValues.isActive ?? true,
                    taxType: taxTypeNorm,
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

            // Category: dropdown value is categoryId (GUID); backend also expects [Required] Category (name)
            const categoryId = values.categoryId as string | undefined;
            if (!categoryId?.trim()) {
                message.error(t('products.messages.categoryPickRequired'));
                return;
            }
            const categoryName =
                categoryOptions.find((o: { value: string }) => o.value === categoryId)?.label ??
                (initialValues as any)?.category ??
                '';

            const rawImageUrl = values.imageUrl;
            const imageUrl =
                rawImageUrl === undefined || rawImageUrl === null || String(rawImageUrl).trim() === ''
                    ? null
                    : String(rawImageUrl).trim();

            const nameDe = String(values.nameDe ?? values.name ?? '').trim();
            const processedValues: ProductFormSubmitValues = {
                ...values,
                name: nameDe || String(values.name ?? '').trim(),
                nameDe: nameDe || undefined,
                nameEn: String(values.nameEn ?? '').trim() || undefined,
                nameTr: String(values.nameTr ?? '').trim() || undefined,
                descriptionDe: String(values.descriptionDe ?? values.description ?? '').trim() || undefined,
                descriptionEn: String(values.descriptionEn ?? '').trim() || undefined,
                descriptionTr: String(values.descriptionTr ?? '').trim() || undefined,
                price: Number(values.price),
                cost: Number(values.cost),
                taxType: Number(values.taxType) as any,
                stockQuantity: Number(values.stockQuantity ?? 0),
                minStockLevel: Number(values.minStockLevel ?? 0),
                unit: values.unit || 'pcs',
                categoryId,
                category: categoryName,
                imageUrl,
                modifierGroupIds: selectedModifierGroupIds,
            };

            await onSubmit(processedValues);
            form.resetFields();
        } catch (error: any) {
            technicalConsole.error('[ProductForm] submit or validation failed', error);

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
            title={initialValues ? t('products.form.titleEdit') : t('products.form.titleCreate')}
            open={visible}
            onOk={handleOk}
            onCancel={onCancel}
            confirmLoading={!!loading || imageUploading}
            okButtonProps={{
                disabled: !!initialValues && modifierGroupsLoading,
            }}
            width={600}
            forceRender
            okText={t('common.buttons.save')}
            cancelText={t('common.buttons.cancel')}
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

                <Collapse
                    defaultActiveKey={['names']}
                    style={{ marginBottom: 16 }}
                    items={[
                        {
                            key: 'names',
                            label: t('products.form.namesMultilingual'),
                            children: (
                                <>
                                    <Form.Item
                                        name="nameDe"
                                        label={t('products.form.nameDe')}
                                        rules={[{ required: true, message: t('products.form.nameRequired') }]}
                                    >
                                        <Input placeholder="Pizza Margherita" />
                                    </Form.Item>
                                    <Form.Item name="nameEn" label={t('products.form.nameEn')}>
                                        <Input placeholder="Margherita Pizza" />
                                    </Form.Item>
                                    <Form.Item name="nameTr" label={t('products.form.nameTr')}>
                                        <Input placeholder="Margherita Pizza" />
                                    </Form.Item>
                                    <Form.Item name="descriptionDe" label={t('products.form.descriptionDe')}>
                                        <TextArea rows={2} placeholder="mit Tomaten und Mozzarella" />
                                    </Form.Item>
                                    <Form.Item name="descriptionEn" label={t('products.form.descriptionEn')}>
                                        <TextArea rows={2} placeholder="with tomatoes and mozzarella" />
                                    </Form.Item>
                                    <Form.Item name="descriptionTr" label={t('products.form.descriptionTr')}>
                                        <TextArea rows={2} />
                                    </Form.Item>
                                </>
                            ),
                        },
                    ]}
                />
                <Form.Item name="name" hidden>
                    <Input />
                </Form.Item>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <Form.Item
                        name="barcode"
                        label={t('products.form.barcode')}
                    >
                        <Input placeholder={t('products.form.barcodePlaceholder')} />
                    </Form.Item>

                    <Form.Item
                        name="categoryId"
                        label={t('products.form.category')}
                        rules={[{ required: true, message: t('products.form.categoryRequired') }]}
                    >
                        <Select
                            placeholder={t('products.form.categoryPlaceholder')}
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
                        label={t('products.form.price')}
                        rules={[{ required: true, message: t('products.form.priceRequired') }]}
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
                        label={t('products.form.cost')}
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
                    label={t('products.form.taxType')}
                    rules={[{ required: true, message: t('products.form.taxTypeRequired') }]}
                >
                    <Select
                        options={taxTypeOptions.map((o) => ({ value: o.value, label: o.label }))}
                        placeholder={t('products.form.taxTypePlaceholder')}
                    />
                </Form.Item>

                <Form.Item
                    name="imageUrl"
                    label={t('products.form.imageUrl')}
                    extra={t('products.form.imageUrlExtra')}
                    rules={productImageUrlRules}
                >
                    <Input
                        allowClear
                        autoComplete="off"
                        placeholder={t('products.form.imageUrlPlaceholder')}
                    />
                </Form.Item>

                <Form.Item label={t('products.form.imageUploadLabel')} extra={t('products.form.imageUploadExtra')}>
                    <Upload
                        accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
                        showUploadList={false}
                        disabled={!initialValues?.id || !!loading || imageUploading}
                        beforeUpload={(file) => {
                            if (file.size > MAX_PRODUCT_IMAGE_BYTES) {
                                message.error(t('products.form.imageUploadFailed'));
                                return Upload.LIST_IGNORE;
                            }
                            return true;
                        }}
                        customRequest={async (options) => {
                            const { file, onError, onSuccess, onProgress } = options;
                            const id = initialValues?.id;
                            if (!id) {
                                onError?.(new Error('no product id'));
                                return;
                            }
                            const f = file as File;
                            setImageUploading(true);
                            setImageUploadPercent(0);
                            try {
                                const url = await uploadAdminProductImage(id, f, {
                                    onProgress: (pct) => {
                                        setImageUploadPercent(pct);
                                        onProgress?.({ percent: pct });
                                    },
                                });
                                form.setFieldsValue({ imageUrl: url });
                                setImageUploadPercent(100);
                                onSuccess?.(url);
                            } catch {
                                message.error(t('products.form.imageUploadFailed'));
                                onError?.(new Error('upload failed'));
                            } finally {
                                setImageUploading(false);
                                setImageUploadPercent(null);
                            }
                        }}
                    >
                        <Button
                            type="default"
                            icon={<UploadOutlined />}
                            loading={imageUploading}
                            disabled={!initialValues?.id || !!loading}
                        >
                            {imageUploading && imageUploadPercent != null
                                ? `${t('products.form.imageUploading')} ${imageUploadPercent}%`
                                : t('products.form.imageUploadButton')}
                        </Button>
                    </Upload>
                    {!initialValues?.id ? (
                        <div style={{ marginTop: 8, fontSize: 12, opacity: 0.75 }}>
                            {t('products.form.imageUploadSaveFirst')}
                        </div>
                    ) : null}
                </Form.Item>

                <Form.Item
                    name="isActive"
                    label={t('products.form.active')}
                    valuePropName="checked"
                >
                    <Switch />
                </Form.Item>

                <Form.Item
                    label={t('products.form.addonGroupsLabel')}
                    extra={t('products.form.addonGroupsExtra')}
                    style={{ marginBottom: 0 }}
                >
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
