'use client';

/**
 * Add-on-Gruppen (Suggested Add-On Groups).
 * Faz 1: Neuer Akfluss über Products; Modifiers bleiben als Legacy sichtbar.
 * Preis/MwSt. nur aus Produktdaten.
 */

import React, { useState } from 'react';
import { Button, Modal, Form, Input, InputNumber, Switch, message, Collapse, Tabs, Select } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getModifierGroups,
  createModifierGroup,
  updateModifierGroup,
  addProductToGroup,
  type ModifierGroupDto,
  type AddOnGroupProductItemDto,
} from '@/lib/api/modifierGroups';
import { getAdminProductsList } from '@/api/admin/products';
import { useCategories } from '@/features/categories/hooks/useCategories';

const modifierGroupsKey = ['modifier-groups'] as const;
const adminProductsListKey = ['admin', 'products', 'list'] as const;

export default function ModifierGroupsPage() {
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
      message.success('Add-on-Gruppe angelegt.');
      setGroupModalOpen(false);
      groupForm.resetFields();
      queryClient.invalidateQueries({ queryKey: modifierGroupsKey });
    } catch (e: any) {
      if (e?.errorFields) return;
      message.error('Gruppe konnte nicht angelegt werden.');
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
      message.success('Gruppe aktualisiert.');
      setEditGroupModalOpen(false);
      setGroupToEdit(null);
      editGroupForm.resetFields();
      queryClient.invalidateQueries({ queryKey: modifierGroupsKey });
    } catch (e: any) {
      if (e?.errorFields) return;
      message.error('Gruppe konnte nicht aktualisiert werden.');
    }
  };

  const handleAddProduct = async () => {
    if (!selectedGroup) return;
    try {
      if (productModalTab === 'existing') {
        const productId = productForm.getFieldValue('productId');
        if (!productId) {
          message.error('Bitte wählen Sie ein Produkt.');
          return;
        }
        await addProductToGroup(selectedGroup.id, { productId });
      } else {
        const values = await productForm.validateFields(['name', 'price', 'taxType', 'categoryId', 'sortOrder']);
        if (!values.categoryId) {
          message.error('Kategorie ist bei neuem Add-on erforderlich.');
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
      message.success('Produkt zur Gruppe hinzugefügt.');
      setProductModalOpen(false);
      setSelectedGroup(null);
      productForm.resetFields();
      queryClient.invalidateQueries({ queryKey: modifierGroupsKey });
    } catch (e: any) {
      if (e?.errorFields) return;
      message.error(e?.message ?? 'Produkt konnte nicht hinzugefügt werden.');
    }
  };

  const productOptions = (productsRes?.items ?? []).map((p) => ({ label: `${p.name} (€${Number(p.price).toFixed(2)})`, value: p.id }));

  const items = groups.map((g) => {
    const products: AddOnGroupProductItemDto[] = g.products ?? [];
    const modifiers = g.modifiers ?? [];
    return {
      key: g.id,
      label: g.name,
      extra: (
        <span>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditGroup(g)}>
            Bearbeiten
          </Button>
          <Button type="link" size="small" onClick={() => openAddProduct(g)}>
            + Produkt
          </Button>
        </span>
      ),
      children: (
        <div style={{ paddingLeft: 8 }}>
          <div style={{ marginBottom: 8, fontWeight: 600, color: '#333' }}>Add-on-Produkte in dieser Gruppe</div>
          {products.length === 0 ? (
            <div style={{ color: '#999', marginBottom: 12 }}>Keine Add-on-Produkte. Klicken Sie auf „+ Produkt“.</div>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20, marginBottom: 12 }}>
              {products.map((p) => (
                <li key={p.productId}>
                  {p.productName} — €{Number(p.price).toFixed(2)} (MwSt.-Typ {p.taxType})
                </li>
              ))}
            </ul>
          )}
          <div style={{ marginBottom: 4, fontSize: 12, color: '#999' }}>Modifier (Legacy, nur Leseansicht — neue bitte als „+ Produkt“ anlegen)</div>
          {modifiers.length === 0 ? (
            <div style={{ color: '#bbb', paddingLeft: 20 }}>Keine Modifier.</div>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20 }}>
              {modifiers.map((m) => (
                <li key={m.id} style={{ color: '#666' }}>
                  {m.name} — €{Number(m.price).toFixed(2)} (MwSt. {m.taxType})
                </li>
              ))}
            </ul>
          )}
        </div>
      ),
    };
  });

  return (
    <div style={{ padding: 24, background: '#fff', borderRadius: 8 }}>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>Add-on-Gruppen</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setGroupModalOpen(true)}>
          Gruppe anlegen
        </Button>
      </div>
      <p style={{ color: '#666', marginBottom: 16 }}>
        Hier verwalten Sie Gruppen (z. B. Saucen, Extras) und deren Add-on-Produkte. Mit „Bearbeiten“ ändern Sie Gruppennamen und Sortierung. Mit „+ Produkt“ fügen Sie verkaufbare Add-on-Produkte zu einer Gruppe hinzu. Welche Gruppen pro Produkt angezeigt werden, legen Sie auf der Produktseite fest.
      </p>
      {isLoading ? (
        <div style={{ padding: 24, textAlign: 'center' }}>Laden…</div>
      ) : (
        <Collapse items={items} defaultActiveKey={groups.map((g) => g.id)} />
      )}

      <Modal
        title="Neue Add-on-Gruppe"
        open={groupModalOpen}
        onOk={handleAddGroup}
        onCancel={() => { setGroupModalOpen(false); groupForm.resetFields(); }}
        okText="Anlegen"
      >
        <Form form={groupForm} layout="vertical" initialValues={{ minSelections: 0, sortOrder: 0, isRequired: false }}>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}>
            <Input placeholder="z. B. Saucen, Extras, Beilagen" />
          </Form.Item>
          <Form.Item name="minSelections" label="Min. Auswahl">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="maxSelections" label="Max. Auswahl (leer = unbegrenzt)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isRequired" label="Pflicht (mind. 1 Auswahl)" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="sortOrder" label="Sortierung">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={groupToEdit ? `Gruppe „${groupToEdit.name}“ bearbeiten` : 'Gruppe bearbeiten'}
        open={editGroupModalOpen}
        onOk={handleEditGroup}
        onCancel={() => { setEditGroupModalOpen(false); setGroupToEdit(null); editGroupForm.resetFields(); }}
        okText="Speichern"
      >
        <Form form={editGroupForm} layout="vertical" initialValues={{ minSelections: 0, sortOrder: 0, isRequired: false }}>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Name ist erforderlich.' }]}>
            <Input placeholder="z. B. Saucen, Extras, Beilagen" maxLength={100} showCount />
          </Form.Item>
          <Form.Item name="minSelections" label="Min. Auswahl">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="maxSelections" label="Max. Auswahl (leer = unbegrenzt)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isRequired" label="Pflicht (mind. 1 Auswahl)" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="sortOrder" label="Sortierung">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={selectedGroup ? `Produkt zu „${selectedGroup.name}“ hinzufügen` : 'Produkt hinzufügen'}
        open={productModalOpen}
        onOk={handleAddProduct}
        onCancel={() => { setProductModalOpen(false); setSelectedGroup(null); productForm.resetFields(); }}
        okText="Hinzufügen"
        width={480}
      >
        <Tabs
          activeKey={productModalTab}
          onChange={(k) => { setProductModalTab(k as 'existing' | 'new'); productForm.resetFields(); }}
          items={[
            {
              key: 'existing',
              label: 'Bestehendes Produkt',
              children: (
                <Form form={productForm} layout="vertical">
                  <Form.Item name="productId" label="Produkt" rules={productModalTab === 'existing' ? [{ required: true, message: 'Bitte Produkt wählen.' }] : []}>
                    <Select
                      showSearch
                      placeholder="Produkt auswählen…"
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
              label: 'Neues Add-on anlegen',
              children: (
                <Form form={productForm} layout="vertical" initialValues={{ price: 0, taxType: 1, sortOrder: 0 }}>
                  <Form.Item name="name" label="Name" rules={productModalTab === 'new' ? [{ required: true }] : []}>
                    <Input placeholder="z. B. Extra Käse" />
                  </Form.Item>
                  <Form.Item name="price" label="Preis (€)" rules={productModalTab === 'new' ? [{ required: true }] : []}>
                    <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item name="taxType" label="MwSt.-Typ (1=20%, 2=10%, 3=13%)">
                    <InputNumber min={1} max={4} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item name="categoryId" label="Kategorie" rules={[{ required: true, message: 'Kategorie ist erforderlich.' }]}>
                    <Select placeholder="Kategorie wählen" options={categoryOptions} />
                  </Form.Item>
                  <Form.Item name="sortOrder" label="Sortierung">
                    <InputNumber min={0} style={{ width: '100%' }} />
                  </Form.Item>
                </Form>
              ),
            },
          ]}
        />
      </Modal>
    </div>
  );
}
