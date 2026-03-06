'use client';

/**
 * Add-on-Gruppen (Suggested Add-On Groups).
 * Faz 1: Neuer Akfluss über Products; Modifiers bleiben als Legacy sichtbar.
 * Preis/MwSt. nur aus Produktdaten.
 */

import React, { useState } from 'react';
import { Button, Modal, Form, Input, InputNumber, Switch, message, Collapse, Tabs, Select } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getModifierGroups,
  createModifierGroup,
  addModifierToGroup,
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
  const [modifierModalOpen, setModifierModalOpen] = useState(false);
  const [productModalOpen, setProductModalOpen] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState<ModifierGroupDto | null>(null);
  const [groupForm] = Form.useForm();
  const [modifierForm] = Form.useForm();
  const [productForm] = Form.useForm();
  const [productModalTab, setProductModalTab] = useState<'existing' | 'new'>('existing');
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

  const openAddModifier = (group: ModifierGroupDto) => {
    setSelectedGroup(group);
    modifierForm.setFieldsValue({ name: '', price: 0, taxType: 1, sortOrder: 0 });
    setModifierModalOpen(true);
  };

  const handleAddModifier = async () => {
    if (!selectedGroup) return;
    try {
      const values = await modifierForm.validateFields();
      await addModifierToGroup(selectedGroup.id, {
        name: values.name,
        price: Number(values.price) || 0,
        taxType: Number(values.taxType) || 1,
        sortOrder: Number(values.sortOrder) || 0,
      });
      message.success('Modifier hinzugefügt (Legacy).');
      setModifierModalOpen(false);
      setSelectedGroup(null);
      modifierForm.resetFields();
      queryClient.invalidateQueries({ queryKey: modifierGroupsKey });
    } catch (e: any) {
      if (e?.errorFields) return;
      message.error('Modifier konnte nicht hinzugefügt werden.');
    }
  };

  const openAddProduct = (group: ModifierGroupDto) => {
    setSelectedGroup(group);
    setProductModalTab('existing');
    productForm.resetFields();
    setProductModalOpen(true);
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
          <Button type="link" size="small" onClick={() => openAddProduct(g)}>
            + Produkt
          </Button>
          <Button type="link" size="small" onClick={() => openAddModifier(g)} style={{ color: '#999' }}>
            + Modifier (Legacy)
          </Button>
        </span>
      ),
      children: (
        <div style={{ paddingLeft: 8 }}>
          <div style={{ marginBottom: 8, fontWeight: 600, color: '#333' }}>Produkte (Preis/MwSt. aus Produktdaten)</div>
          {products.length === 0 ? (
            <div style={{ color: '#999', marginBottom: 12 }}>Keine Produkte. Klicken Sie auf „+ Produkt“.</div>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20, marginBottom: 12 }}>
              {products.map((p) => (
                <li key={p.productId}>
                  {p.productName} — €{Number(p.price).toFixed(2)} (MwSt.-Typ {p.taxType})
                </li>
              ))}
            </ul>
          )}
          <div style={{ marginBottom: 4, fontSize: 12, color: '#999' }}>Modifier (Legacy)</div>
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
        Vorgeschlagene Add-on-Gruppen: Hier legen Sie Produkte pro Gruppe fest. Preis und MwSt. kommen ausschließlich aus den Produktdaten. Anschließend können Sie in Produkten unter „Vorgeschlagene Add-on-Gruppen“ festlegen, welche Gruppen pro Produkt wählbar sind.
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
        title={selectedGroup ? `Modifier in „${selectedGroup.name}“ (Legacy)` : 'Modifier hinzufügen'}
        open={modifierModalOpen}
        onOk={handleAddModifier}
        onCancel={() => { setModifierModalOpen(false); setSelectedGroup(null); modifierForm.resetFields(); }}
        okText="Hinzufügen"
      >
        <Form form={modifierForm} layout="vertical" initialValues={{ price: 0, taxType: 1, sortOrder: 0 }}>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}>
            <Input placeholder="z. B. Ketchup, Mayo" />
          </Form.Item>
          <Form.Item name="price" label="Preis (€)">
            <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="taxType" label="MwSt.-Typ (1=20%, 2=10%, 3=13%)">
            <InputNumber min={1} max={4} style={{ width: '100%' }} />
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
