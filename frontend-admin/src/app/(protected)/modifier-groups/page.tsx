'use client';

/**
 * Add-on groups (modifier groups). Create/edit groups and manage add-on products.
 * Product–group assignment is configured on the product page.
 */

import React, { useState } from 'react';
import { Button, Modal, Form, Input, InputNumber, Switch, message, Collapse, Tabs, Select, Popconfirm, Space, Card, Spin, Typography } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getModifierGroups,
  createModifierGroup,
  updateModifierGroup,
  addProductToGroup,
  removeProductFromGroup,
  type ModifierGroupDto,
  type AddOnGroupProductItemDto,
} from '@/lib/api/modifierGroups';
import { getAdminProductsList } from '@/api/admin/products';
import { useCategories } from '@/features/categories/hooks/useCategories';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const modifierGroupsKey = ['modifier-groups'] as const;
const adminProductsListKey = ['admin', 'products', 'list'] as const;

export default function ModifierGroupsPage() {
  const { t } = useI18n();
  const [groupModalOpen, setGroupModalOpen] = useState(false);
  const [productModalOpen, setProductModalOpen] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState<ModifierGroupDto | null>(null);
  const [groupForm] = Form.useForm();
  const [editGroupForm] = Form.useForm();
  const [productForm] = Form.useForm();
  const [productModalTab, setProductModalTab] = useState<'existing' | 'new'>('existing');
  const [editGroupModalOpen, setEditGroupModalOpen] = useState(false);
  const [groupToEdit, setGroupToEdit] = useState<ModifierGroupDto | null>(null);
  const queryClient = useQueryClient();

  const { useList } = useCategories();
  const { data: categoryList } = useList();

  const { data: groups = [], isLoading } = useQuery({
    queryKey: modifierGroupsKey,
    queryFn: getModifierGroups,
  });

  const { data: productsRes } = useQuery({
    queryKey: [...adminProductsListKey, { pageSize: 500 }],
    queryFn: () => getAdminProductsList({ pageSize: 500 }),
    enabled: productModalOpen && productModalTab === 'existing',
  });

  const categoryOptions = (categoryList ?? []).map((c: { id?: string; name?: string }) => ({ label: c.name ?? '', value: c.id ?? '' })).filter((o: { value: string }) => o.value);

  const handleAddGroup = async () => {
    try {
      const values = await groupForm.validateFields();
      await createModifierGroup({
        name: values.name,
        minSelections: values.minSelections ?? 0,
        maxSelections: values.maxSelections ?? null,
        isRequired: values.isRequired ?? false,
        sortOrder: values.sortOrder ?? 0,
      });
      message.success(t('modifierGroups.messages.groupCreated'));
      setGroupModalOpen(false);
      groupForm.resetFields();
      await queryClient.refetchQueries({ queryKey: modifierGroupsKey });
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      openApiErrorMessage(message.open, t, e, {
        logContext: 'ModifierGroups.createGroup',
        fallbackKey: 'common.messages.unknownError',
      });
    }
  };

  const openAddProduct = (group: ModifierGroupDto) => {
    setSelectedGroup(group);
    setProductModalTab('existing');
    productForm.resetFields();
    setProductModalOpen(true);
  };

  const openEditGroup = (group: ModifierGroupDto) => {
    setGroupToEdit(group);
    editGroupForm.setFieldsValue({
      name: group.name,
      minSelections: group.minSelections ?? 0,
      maxSelections: group.maxSelections ?? undefined,
      isRequired: group.isRequired ?? false,
      sortOrder: group.sortOrder ?? 0,
    });
    setEditGroupModalOpen(true);
  };

  const handleEditGroup = async () => {
    if (!groupToEdit) return;
    try {
      const values = await editGroupForm.validateFields();
      await updateModifierGroup(groupToEdit.id, {
        name: values.name,
        minSelections: values.minSelections ?? 0,
        maxSelections: values.maxSelections ?? null,
        isRequired: values.isRequired ?? false,
        sortOrder: values.sortOrder ?? 0,
      });
      message.success(t('modifierGroups.messages.groupUpdated'));
      setEditGroupModalOpen(false);
      setGroupToEdit(null);
      editGroupForm.resetFields();
      await queryClient.refetchQueries({ queryKey: modifierGroupsKey });
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      openApiErrorMessage(message.open, t, e, {
        logContext: 'ModifierGroups.updateGroup',
        fallbackKey: 'common.messages.unknownError',
      });
    }
  };

  const handleRemoveProduct = async (group: ModifierGroupDto, productId: string) => {
    if (!productId?.trim() || !group?.id) {
      message.error(t('modifierGroups.messages.invalidProductOrGroup'));
      return;
    }
    try {
      await removeProductFromGroup(group.id, productId);
      await queryClient.refetchQueries({ queryKey: modifierGroupsKey });
      message.success(t('modifierGroups.messages.productRemoved'));
    } catch (e: unknown) {
      openApiErrorMessage(message.open, t, e, {
        logContext: 'ModifierGroups.removeProduct',
        fallbackKey: 'common.messages.unknownError',
      });
    }
  };

  const handleAddProduct = async () => {
    if (!selectedGroup) return;
    try {
      if (productModalTab === 'existing') {
        const productId = productForm.getFieldValue('productId');
        if (!productId) {
          message.error(t('modifierGroups.messages.selectProduct'));
          return;
        }
        await addProductToGroup(selectedGroup.id, { productId });
      } else {
        const values = await productForm.validateFields(['name', 'price', 'taxType', 'categoryId', 'sortOrder']);
        if (!values.categoryId) {
          message.error(t('modifierGroups.messages.categoryRequiredNewAddon'));
          return;
        }
        await addProductToGroup(selectedGroup.id, {
          createNewAddOnProduct: {
            name: values.name,
            price: Number(values.price) ?? 0,
            taxType: Number(values.taxType) ?? 1,
            categoryId: values.categoryId,
            sortOrder: Number(values.sortOrder) ?? 0,
          },
        });
      }
      message.success(t('modifierGroups.messages.productAdded'));
      setProductModalOpen(false);
      setSelectedGroup(null);
      productForm.resetFields();
      await queryClient.refetchQueries({ queryKey: modifierGroupsKey });
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      openApiErrorMessage(message.open, t, e, {
        logContext: 'ModifierGroups.addProduct',
        fallbackKey: 'common.messages.unknownError',
      });
    }
  };

  const productOptions = (productsRes?.items ?? []).map((p) => ({ label: `${p.name} (€${Number(p.price).toFixed(2)})`, value: p.id }));

  const items = groups.map((g) => {
    const products: AddOnGroupProductItemDto[] = (g as { products?: AddOnGroupProductItemDto[]; Products?: AddOnGroupProductItemDto[] }).products ?? (g as { products?: AddOnGroupProductItemDto[]; Products?: AddOnGroupProductItemDto[] }).Products ?? [];
    return {
      key: g.id,
      label: g.name,
      extra: (
        <span>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditGroup(g)}>
            {t('modifierGroups.actions.edit')}
          </Button>
          <Button type="link" size="small" onClick={() => openAddProduct(g)}>
            {t('modifierGroups.actions.addProduct')}
          </Button>
        </span>
      ),
      children: (
        <div style={{ paddingLeft: 8 }}>
          <div style={{ marginBottom: 4, fontWeight: 600, color: '#1890ff' }}>{t('modifierGroups.collapse.productsTitle')}</div>
          <div style={{ marginBottom: 8, fontSize: 12, color: '#666' }}>{t('modifierGroups.collapse.productsHint')}</div>
          {products.length === 0 ? (
            <div style={{ color: '#999', marginBottom: 12 }}>{t('modifierGroups.collapse.emptyProducts')}</div>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20, marginBottom: 12 }}>
              {products.map((p) => {
                const productId = (p as { productId?: string; ProductId?: string }).productId ?? (p as { productId?: string; ProductId?: string }).ProductId ?? '';
                return (
                <li key={productId} style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                  <span style={{ flex: 1 }}>
                    {p.productName} — €{Number(p.price).toFixed(2)} ({t('modifierGroups.collapse.taxTypeSuffix', { type: p.taxType })})
                  </span>
                  <span onClick={(e) => e.stopPropagation()}>
                    <Popconfirm
                      title={t('modifierGroups.actions.removeTitle')}
                      description={t('modifierGroups.collapse.popconfirmRemoveDescription')}
                      onConfirm={() => handleRemoveProduct(g, productId)}
                      okText={t('modifierGroups.actions.remove')}
                      cancelText={t('common.buttons.cancel')}
                    >
                      <Button
                        type="link"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                        title={t('modifierGroups.actions.removeTitle')}
                      >
                        {t('modifierGroups.actions.remove')}
                      </Button>
                    </Popconfirm>
                  </span>
                </li>
              );
            })}
            </ul>
          )}
        </div>
      ),
    };
  });

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('modifierGroups.page.title')}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.modifierGroups }]}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setGroupModalOpen(true)}>
            {t('modifierGroups.actions.addGroup')}
          </Button>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('modifierGroups.page.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {isLoading ? (
        <Card>
          <div style={{ textAlign: 'center', padding: '48px 24px' }}>
            <Spin size="large" />
            <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
              {t('modifierGroups.page.loading')}
            </Typography.Paragraph>
          </div>
        </Card>
      ) : (
        <Collapse items={items} defaultActiveKey={groups.map((g) => g.id)} />
      )}

      <Modal
        title={t('modifierGroups.modal.newGroupTitle')}
        open={groupModalOpen}
        onOk={handleAddGroup}
        onCancel={() => { setGroupModalOpen(false); groupForm.resetFields(); }}
        okText={t('modifierGroups.modal.okCreate')}
        cancelText={t('common.buttons.cancel')}
      >
        <Form form={groupForm} layout="vertical" initialValues={{ minSelections: 0, sortOrder: 0, isRequired: false }}>
          <Form.Item name="name" label={t('modifierGroups.form.name')} rules={[{ required: true }]}>
            <Input placeholder={t('modifierGroups.form.placeholderGroupName')} />
          </Form.Item>
          <Form.Item name="minSelections" label={t('modifierGroups.form.minSelections')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="maxSelections" label={t('modifierGroups.form.maxSelections')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isRequired" label={t('modifierGroups.form.isRequired')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="sortOrder" label={t('modifierGroups.form.sortOrder')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={
          groupToEdit
            ? t('modifierGroups.modal.editGroupTitle', { name: groupToEdit.name })
            : t('modifierGroups.modal.editGroupTitleFallback')
        }
        open={editGroupModalOpen}
        onOk={handleEditGroup}
        onCancel={() => { setEditGroupModalOpen(false); setGroupToEdit(null); editGroupForm.resetFields(); }}
        okText={t('modifierGroups.modal.okSave')}
        cancelText={t('common.buttons.cancel')}
      >
        <Form form={editGroupForm} layout="vertical" initialValues={{ minSelections: 0, sortOrder: 0, isRequired: false }}>
          <Form.Item name="name" label={t('modifierGroups.form.name')} rules={[{ required: true, message: t('modifierGroups.form.nameRequired') }]}>
            <Input placeholder={t('modifierGroups.form.placeholderGroupName')} maxLength={100} showCount />
          </Form.Item>
          <Form.Item name="minSelections" label={t('modifierGroups.form.minSelections')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="maxSelections" label={t('modifierGroups.form.maxSelections')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isRequired" label={t('modifierGroups.form.isRequired')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="sortOrder" label={t('modifierGroups.form.sortOrder')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={
          selectedGroup
            ? t('modifierGroups.modal.addProductTitle', { name: selectedGroup.name })
            : t('modifierGroups.modal.addProductTitleFallback')
        }
        open={productModalOpen}
        onOk={handleAddProduct}
        onCancel={() => { setProductModalOpen(false); setSelectedGroup(null); productForm.resetFields(); }}
        okText={t('modifierGroups.modal.okAdd')}
        cancelText={t('common.buttons.cancel')}
        width={480}
      >
        <Tabs
          activeKey={productModalTab}
          onChange={(k) => { setProductModalTab(k as 'existing' | 'new'); productForm.resetFields(); }}
          items={[
            {
              key: 'existing',
              label: t('modifierGroups.tabs.existingProduct'),
              children: (
                <Form form={productForm} layout="vertical">
                  <Form.Item
                    name="productId"
                    label={t('modifierGroups.form.product')}
                    rules={productModalTab === 'existing' ? [{ required: true, message: t('modifierGroups.form.selectProductRequired') }] : []}
                  >
                    <Select
                      showSearch
                      placeholder={t('modifierGroups.form.selectProductPlaceholder')}
                      options={productOptions}
                      filterOption={(input, opt) => (opt?.label ?? '').toString().toLowerCase().includes(input.toLowerCase())}
                      loading={!productsRes}
                    />
                  </Form.Item>
                </Form>
              ),
            },
            {
              key: 'new',
              label: t('modifierGroups.tabs.newAddon'),
              children: (
                <Form form={productForm} layout="vertical" initialValues={{ price: 0, taxType: 1, sortOrder: 0 }}>
                  <Form.Item name="name" label={t('modifierGroups.form.name')} rules={productModalTab === 'new' ? [{ required: true }] : []}>
                    <Input placeholder={t('modifierGroups.form.placeholderNewAddonName')} />
                  </Form.Item>
                  <Form.Item name="price" label={t('modifierGroups.form.price')} rules={productModalTab === 'new' ? [{ required: true }] : []}>
                    <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item name="taxType" label={t('modifierGroups.form.taxType')}>
                    <InputNumber min={1} max={4} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item name="categoryId" label={t('modifierGroups.form.category')} rules={[{ required: true, message: t('modifierGroups.form.categoryRequired') }]}>
                    <Select placeholder={t('modifierGroups.form.placeholderCategory')} options={categoryOptions} />
                  </Form.Item>
                  <Form.Item name="sortOrder" label={t('modifierGroups.form.sortOrder')}>
                    <InputNumber min={0} style={{ width: '100%' }} />
                  </Form.Item>
                </Form>
              ),
            },
          ]}
        />
      </Modal>
    </Space>
  );
}
