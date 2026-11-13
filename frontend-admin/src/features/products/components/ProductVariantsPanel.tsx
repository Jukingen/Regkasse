'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, InputNumber, Space, Table, Tag } from 'antd';
import React, { useState } from 'react';

import {
  adminProductVariantQueryKeys,
  createAdminProductVariant,
  deactivateAdminProductVariant,
  updateAdminProductVariant,
  useAdminProductVariants,
  type ProductVariant,
  type ProductVariantWriteBody,
} from '@/api/admin/product-variants';
import { ensureProductVariantApiErrorTranslations } from '@/features/products/productVariantApiErrors';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

ensureProductVariantApiErrorTranslations();

interface VariantFormValues {
  sku: string;
  variantLabel: string;
  priceDelta: number;
  stock: number;
}

interface ProductVariantsPanelProps {
  productId: string;
}

export default function ProductVariantsPanel({ productId }: ProductVariantsPanelProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<VariantFormValues>();
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const variantsQuery = useAdminProductVariants(productId, true);

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: adminProductVariantQueryKeys.byProduct(productId) });

  const save = useMutation({
    mutationFn: async (values: VariantFormValues) => {
      const body: ProductVariantWriteBody = {
        sku: values.sku.trim(),
        variantLabel: values.variantLabel.trim(),
        priceDelta: Number(values.priceDelta ?? 0),
        stock: Number(values.stock ?? 0),
        isActive: true,
      };
      if (editingId) {
        const current = (variantsQuery.data ?? []).find((row) => row.id === editingId);
        body.isActive = current?.isActive ?? true;
        return updateAdminProductVariant(productId, editingId, body);
      }
      return createAdminProductVariant(productId, body);
    },
    onSuccess: async () => {
      notify.successKey('products.variants.saved');
      setEditorOpen(false);
      setEditingId(null);
      form.resetFields();
      await invalidate();
    },
    onError: (error) => {
      notify.apiError(error, { logContext: 'ProductVariants.save', fallbackKey: 'common.errorGeneric' });
    },
  });

  const deactivate = useMutation({
    mutationFn: (variantId: string) => deactivateAdminProductVariant(productId, variantId),
    onSuccess: async () => {
      notify.successKey('products.variants.deactivated');
      await invalidate();
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'ProductVariants.deactivate',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const openCreate = () => {
    setEditingId(null);
    form.setFieldsValue({ sku: '', variantLabel: '', priceDelta: 0, stock: 0 });
    setEditorOpen(true);
  };

  const openEdit = (row: ProductVariant) => {
    setEditingId(row.id);
    form.setFieldsValue({
      sku: row.sku,
      variantLabel: row.variantLabel,
      priceDelta: row.priceDelta,
      stock: row.stock,
    });
    setEditorOpen(true);
  };

  return (
    <div>
      <div style={{ marginBottom: 12 }}>
        <Button type="primary" onClick={openCreate}>
          {t('products.variants.new')}
        </Button>
      </div>

      {editorOpen ? (
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => save.mutate(values)}
          style={{ marginBottom: 16 }}
        >
          <Form.Item
            name="sku"
            label={t('products.variants.sku')}
            rules={[{ required: true, whitespace: true, message: t('products.variants.skuRequired') }]}
          >
            <Input maxLength={64} />
          </Form.Item>
          <Form.Item
            name="variantLabel"
            label={t('products.variants.label')}
            rules={[{ required: true, whitespace: true, message: t('products.variants.labelRequired') }]}
          >
            <Input maxLength={128} />
          </Form.Item>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Form.Item name="priceDelta" label={t('products.variants.priceDelta')}>
              <InputNumber style={{ width: '100%' }} precision={2} />
            </Form.Item>
            <Form.Item name="stock" label={t('products.variants.stock')}>
              <InputNumber style={{ width: '100%' }} min={0} precision={0} />
            </Form.Item>
          </div>
          <Space>
            <Button type="primary" htmlType="submit" loading={save.isPending}>
              {t('products.variants.save')}
            </Button>
            <Button
              onClick={() => {
                setEditorOpen(false);
                setEditingId(null);
              }}
            >
              {t('products.variants.cancel')}
            </Button>
          </Space>
        </Form>
      ) : null}

      <Table
        size="small"
        rowKey="id"
        loading={variantsQuery.isLoading}
        pagination={false}
        dataSource={variantsQuery.data ?? []}
        locale={{ emptyText: t('products.variants.empty') }}
        columns={[
          { title: t('products.variants.sku'), dataIndex: 'sku' },
          { title: t('products.variants.label'), dataIndex: 'variantLabel' },
          { title: t('products.variants.priceDelta'), dataIndex: 'priceDelta' },
          { title: t('products.variants.stock'), dataIndex: 'stock' },
          {
            title: t('products.variants.active'),
            dataIndex: 'isActive',
            render: (isActive: boolean) => (
              <Tag variant="filled">
                {isActive ? t('products.variants.active') : t('products.variants.inactive')}
              </Tag>
            ),
          },
          {
            title: '',
            key: 'actions',
            render: (_: unknown, row: ProductVariant) => (
              <Space>
                <Button size="small" onClick={() => openEdit(row)}>
                  {t('products.variants.edit')}
                </Button>
                {row.isActive ? (
                  <Button
                    size="small"
                    danger
                    onClick={() =>
                      modal.confirm({
                        title: t('products.variants.deactivateTitle'),
                        content: t('products.variants.deactivateConfirm'),
                        okText: t('products.variants.deactivate'),
                        cancelText: t('products.variants.cancel'),
                        onOk: () => deactivate.mutateAsync(row.id),
                      })
                    }
                  >
                    {t('products.variants.deactivate')}
                  </Button>
                ) : null}
              </Space>
            ),
          },
        ]}
      />
    </div>
  );
}
