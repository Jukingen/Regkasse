'use client';

import { UploadOutlined } from '@ant-design/icons';
import { Alert, Button, Collapse, Form, Input, InputNumber, Modal, Select, Switch, Upload } from 'antd';
import React, { useEffect, useMemo, useState } from 'react';

import { MAX_PRODUCT_IMAGE_BYTES, uploadAdminProductImage } from '@/api/admin/products';
import { Product } from '@/api/generated/model';
import { OptimizedImage } from '@/components/OptimizedImage';
import { TaxSelect } from '@/components/TaxSelect';
import { useCategories } from '@/features/categories/hooks/useCategories';
import { taxRateToType } from '@/features/products/utils/productMapper';
import { PriceChangeModal } from '@/features/tax/components/PriceChangeModal';
import { PriceHistoryCard } from '@/features/tax/components/PriceHistoryCard';
import type { PriceChangeResult } from '@/features/tax/api/priceHistory';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useCurrentTaxRegulation } from '@/hooks/useCurrentTaxRegulation';
import { resolveTaxGroupForProduct, useTaxGroups } from '@/hooks/useTaxGroups';
import { useI18n } from '@/i18n';
import {
  type ModifierGroupDto,
  getModifierGroups,
  getProductModifierGroups,
} from '@/lib/api/modifierGroups';
import { technicalConsole } from '@/shared/dev/technicalConsole';

import ExtraZutatenSection from './ExtraZutatenSection';

export type ProductFormSubmitValues = Product & {
  modifierGroupIds?: string[];
  categoryId?: string;
  taxGroupId?: string;
  taxRate?: number;
};

interface ProductFormProps {
  visible: boolean;
  initialValues?: Product | null;
  /** True while editing an existing product (detail may still be loading). */
  isEditMode?: boolean;
  onCancel: () => void;
  onSubmit: (values: ProductFormSubmitValues) => Promise<void>;
  /** Called after RKSV-safe price change (may switch to a new catalog product id). */
  onPriceChanged?: (result: PriceChangeResult) => void | Promise<void>;
  loading?: boolean;
}

const { TextArea } = Input;

/** Aligned with backend Product model / AdminProductsController field limits. */
const PRODUCT_NAME_MAX_LENGTH = 200;
const PRODUCT_DESCRIPTION_MAX_LENGTH = 2000;
const PRODUCT_BARCODE_MAX_LENGTH = 50;

export default function ProductForm(props: ProductFormProps) {
  if (!props.visible) {
    return null;
  }
  return <ProductFormContent {...props} />;
}

function ProductFormContent({
  visible,
  initialValues,
  isEditMode = false,
  onCancel,
  onSubmit,
  onPriceChanged,
  loading,
}: ProductFormProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const [form] = Form.useForm();
  const watchedImageUrl = Form.useWatch('imageUrl', form) as string | undefined;
  const [modifierGroups, setModifierGroups] = useState<ModifierGroupDto[]>([]);
  const [selectedModifierGroupIds, setSelectedModifierGroupIds] = useState<string[]>([]);
  const [modifierGroupsLoading, setModifierGroupsLoading] = useState(false);
  const [imageUploading, setImageUploading] = useState(false);
  const [imageUploadPercent, setImageUploadPercent] = useState<number | null>(null);
  const [priceChangeOpen, setPriceChangeOpen] = useState(false);

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
          assignedGroups
            .map((g) =>
              String(
                (g as { id?: string; Id?: string }).id ??
                  (g as { id?: string; Id?: string }).Id ??
                  ''
              )
            )
            .filter(Boolean)
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
    return () => {
      cancelled = true;
    };
  }, [visible, initialValues?.id, t]);

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
    [t]
  );

  const { data: taxGroups } = useTaxGroups(visible);
  const { data: regulation } = useCurrentTaxRegulation(visible);
  const watchedTaxGroupId = Form.useWatch('taxGroupId', form) as string | undefined;
  const watchedPrice = Form.useWatch('price', form) as number | undefined;
  const watchedName = Form.useWatch('name', form) as string | undefined;

  const selectedTaxRate = useMemo(() => {
    if (!watchedTaxGroupId || !taxGroups?.length) return null;
    const group = taxGroups.find((g) => g.id === watchedTaxGroupId);
    return group?.rate ?? null;
  }, [watchedTaxGroupId, taxGroups]);

  const isValidTaxRate =
    selectedTaxRate == null || regulation == null || regulation.isTaxRateValid(selectedTaxRate);

  const nameMaxLengthRule = useMemo(
    () => ({
      max: PRODUCT_NAME_MAX_LENGTH,
      message: t('products.form.fieldMaxLength', { max: PRODUCT_NAME_MAX_LENGTH }),
    }),
    [t]
  );

  const descriptionMaxLengthRule = useMemo(
    () => ({
      max: PRODUCT_DESCRIPTION_MAX_LENGTH,
      message: t('products.form.fieldMaxLength', { max: PRODUCT_DESCRIPTION_MAX_LENGTH }),
    }),
    [t]
  );

  const barcodeMaxLengthRule = useMemo(
    () => ({
      max: PRODUCT_BARCODE_MAX_LENGTH,
      message: t('products.form.fieldMaxLength', { max: PRODUCT_BARCODE_MAX_LENGTH }),
    }),
    [t]
  );

  // Category list: /api/admin/categories (useCategories → src/api/admin/categories)
  const { useList } = useCategories();
  const { data: categoryList } = useList();

  const categoryOptions = useMemo(() => {
    const list = categoryList ?? [];
    return list
      .map((cat: { id?: string; name?: string }) => ({
        label: cat.name ?? '',
        value: cat.id ?? '',
      }))
      .filter((o: { value: string }) => o.value);
  }, [categoryList]);

  useEffect(() => {
    if (visible) {
      if (initialValues) {
        const product = initialValues as Product & {
          categoryId?: string;
          taxGroupId?: string | null;
          taxRate?: number;
          taxGroup?: { id?: string } | null;
        };
        const categoryId =
          product.categoryId ??
          (
            categoryOptions.find((o: { label: string }) => o.label === (product.category ?? '')) as
              { value: string } | undefined
          )?.value;

        const resolvedTaxGroup = resolveTaxGroupForProduct(taxGroups, {
          taxGroupId: product.taxGroupId ?? product.taxGroup?.id ?? null,
          taxRate: product.taxRate ?? null,
        });
        const defaultTaxGroup =
          resolvedTaxGroup ?? taxGroups?.find((g) => g.isDefault && g.isActive) ?? taxGroups?.[0];

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
          taxGroupId: defaultTaxGroup?.id,
          unit: initialValues.unit || 'pcs',
          stockQuantity: initialValues.stockQuantity ?? 0,
          minStockLevel: initialValues.minStockLevel ?? 0,
          categoryId: categoryId || undefined,
        });
      } else if (!isEditMode) {
        form.resetFields();
        const defaultTaxGroup =
          taxGroups?.find((g) => g.isDefault && g.isActive) ?? taxGroups?.[0];
        form.setFieldsValue({
          isActive: true,
          taxGroupId: defaultTaxGroup?.id,
          price: 0,
          cost: 0,
          unit: 'pcs',
          stockQuantity: 0,
          minStockLevel: 0,
        });
      }
    }
  }, [visible, initialValues, form, categoryOptions, isEditMode, taxGroups]);

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
        initialValues?.category ??
        '';

      const rawImageUrl = values.imageUrl;
      const imageUrl =
        rawImageUrl === undefined || rawImageUrl === null || String(rawImageUrl).trim() === ''
          ? null
          : String(rawImageUrl).trim();

      const nameDe = String(values.nameDe ?? values.name ?? '').trim();
      const taxGroupId = values.taxGroupId as string | undefined;
      const selectedTaxGroup = taxGroups?.find((g) => g.id === taxGroupId);
      if (!taxGroupId || !selectedTaxGroup) {
        message.error(t('products.form.taxGroupRequired'));
        return;
      }

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
        taxGroupId,
        taxType: taxRateToType(selectedTaxGroup.rate) as unknown as Product['taxType'],
        taxRate: selectedTaxGroup.rate,
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
    } catch (error: unknown) {
      technicalConsole.error('[ProductForm] submit or validation failed', error);

      type AxiosLikeValidationError = {
        response?: { data?: { errors?: Record<string, string[]>; title?: string } };
      };
      const axiosError = error as AxiosLikeValidationError;

      // Handle Backend Validation Errors
      if (axiosError.response?.data?.errors) {
        // Map Backend Validation Errors to AntD Form
        const validationErrors = axiosError.response.data.errors;
        const formErrors = Object.keys(validationErrors).map((key) => {
          // Convert PascalCase (e.g. "Unit") to camelCase (e.g. "unit")
          const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
          return {
            name: camelKey,
            errors: validationErrors[key],
          };
        });
        form.setFields(formErrors);

        // If there's an error on a hidden field (shouldn't happen with defaults, but just in case),
        // we might want to know.
      } else if (axiosError.response?.data?.title) {
        message.error(axiosError.response.data.title);
      }
    }
  };

  return (
    <>
    <Modal
      title={isEditMode ? t('products.form.titleEdit') : t('products.form.titleCreate')}
      open={visible}
      onOk={handleOk}
      onCancel={onCancel}
      confirmLoading={!!loading || imageUploading}
      okButtonProps={{
        disabled: (isEditMode && !initialValues) || (!!isEditMode && modifierGroupsLoading),
      }}
      width={600}
      forceRender
      okText={t('common.buttons.save')}
      cancelText={t('common.buttons.cancel')}
    >
      <Form form={form} layout="vertical" initialValues={{ isActive: true }}>
        {/* Hidden Fields to hold state */}
        <Form.Item name="unit" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="stockQuantity" hidden>
          <InputNumber />
        </Form.Item>
        <Form.Item name="minStockLevel" hidden>
          <InputNumber />
        </Form.Item>

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
                    rules={[
                      { required: true, message: t('products.form.nameRequired') },
                      nameMaxLengthRule,
                    ]}
                  >
                    <Input placeholder="Pizza Margherita" maxLength={PRODUCT_NAME_MAX_LENGTH} />
                  </Form.Item>
                  <Form.Item
                    name="nameEn"
                    label={t('products.form.nameEn')}
                    rules={[nameMaxLengthRule]}
                  >
                    <Input placeholder="Margherita Pizza" maxLength={PRODUCT_NAME_MAX_LENGTH} />
                  </Form.Item>
                  <Form.Item
                    name="nameTr"
                    label={t('products.form.nameTr')}
                    rules={[nameMaxLengthRule]}
                  >
                    <Input placeholder="Margherita Pizza" maxLength={PRODUCT_NAME_MAX_LENGTH} />
                  </Form.Item>
                  <Form.Item
                    name="descriptionDe"
                    label={t('products.form.descriptionDe')}
                    rules={[descriptionMaxLengthRule]}
                  >
                    <TextArea
                      rows={2}
                      placeholder="mit Tomaten und Mozzarella"
                      maxLength={PRODUCT_DESCRIPTION_MAX_LENGTH}
                      showCount
                    />
                  </Form.Item>
                  <Form.Item
                    name="descriptionEn"
                    label={t('products.form.descriptionEn')}
                    rules={[descriptionMaxLengthRule]}
                  >
                    <TextArea
                      rows={2}
                      placeholder="with tomatoes and mozzarella"
                      maxLength={PRODUCT_DESCRIPTION_MAX_LENGTH}
                      showCount
                    />
                  </Form.Item>
                  <Form.Item
                    name="descriptionTr"
                    label={t('products.form.descriptionTr')}
                    rules={[descriptionMaxLengthRule]}
                  >
                    <TextArea rows={2} maxLength={PRODUCT_DESCRIPTION_MAX_LENGTH} showCount />
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
            rules={[barcodeMaxLengthRule]}
          >
            <Input
              placeholder={t('products.form.barcodePlaceholder')}
              maxLength={PRODUCT_BARCODE_MAX_LENGTH}
            />
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
            extra={
              isEditMode && initialValues?.id
                ? t('products.priceChange.editViaModalHint')
                : undefined
            }
          >
            <InputNumber
              style={{ width: '100%' }}
              min={0}
              precision={2}
              prefix="€"
              disabled={isEditMode && !!initialValues?.id}
            />
          </Form.Item>

          <Form.Item name="cost" label={t('products.form.cost')}>
            <InputNumber style={{ width: '100%' }} min={0} precision={2} prefix="€" />
          </Form.Item>
        </div>

        <Form.Item
          name="taxGroupId"
          label={t('products.form.taxGroup')}
          rules={[{ required: true, message: t('products.form.taxGroupRequired') }]}
          extra={
            isEditMode && initialValues?.id
              ? t('products.priceChange.editViaModalHint')
              : undefined
          }
        >
          <TaxSelect disabled={isEditMode && !!initialValues?.id} />
        </Form.Item>

        {isEditMode && initialValues?.id ? (
          <div style={{ marginBottom: 16 }}>
            <Button type="default" onClick={() => setPriceChangeOpen(true)}>
              {t('products.priceChange.openButton')}
            </Button>
          </div>
        ) : null}

        {!isValidTaxRate ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('products.form.taxRateInvalidTitle')}
            description={t('products.form.taxRateInvalidDescription')}
          />
        ) : null}

        {isEditMode && initialValues?.id ? <PriceHistoryCard productId={initialValues.id} /> : null}

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

        {typeof watchedImageUrl === 'string' && watchedImageUrl.trim() ? (
          <div style={{ marginBottom: 16 }}>
            <OptimizedImage
              src={watchedImageUrl.trim()}
              alt=""
              width={120}
              height={120}
              style={{ objectFit: 'contain', borderRadius: 8 }}
            />
          </div>
        ) : null}

        <Form.Item
          label={t('products.form.imageUploadLabel')}
          extra={t('products.form.imageUploadExtra')}
        >
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

        <Form.Item name="isActive" label={t('products.form.active')} valuePropName="checked">
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

      {isEditMode && initialValues?.id ? (
        <PriceChangeModal
          open={priceChangeOpen}
          productId={initialValues.id}
          productName={watchedName || (initialValues as { name?: string }).name}
          currentPrice={Number(watchedPrice ?? initialValues.price ?? 0)}
          currentTaxGroupId={String(
            watchedTaxGroupId ?? (initialValues as { taxGroupId?: string }).taxGroupId ?? ''
          )}
          currentTaxRate={Number(selectedTaxRate ?? initialValues.taxRate ?? 0)}
          onClose={() => setPriceChangeOpen(false)}
          onSuccess={async (result) => {
            if (result.newPrice != null) {
              form.setFieldsValue({ price: result.newPrice });
            }
            if (result.newTaxGroupId) {
              form.setFieldsValue({ taxGroupId: result.newTaxGroupId });
            }
            await onPriceChanged?.(result);
          }}
        />
      ) : null}
    </>
  );
}
