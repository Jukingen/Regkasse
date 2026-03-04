'use client';

import React, { useState } from 'react';
import { Button, Table, Modal, Form, Input, InputNumber, Switch, message, Collapse } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getModifierGroups,
  createModifierGroup,
  addModifierToGroup,
  type ModifierGroupDto,
} from '@/lib/api/modifierGroups';

const modifierGroupsKey = ['modifier-groups'] as const;

export default function ModifierGroupsPage() {
  const [groupModalOpen, setGroupModalOpen] = useState(false);
  const [modifierModalOpen, setModifierModalOpen] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState<ModifierGroupDto | null>(null);
  const [groupForm] = Form.useForm();
  const [modifierForm] = Form.useForm();
  const queryClient = useQueryClient();

  const { data: groups = [], isLoading } = useQuery({
    queryKey: modifierGroupsKey,
    queryFn: getModifierGroups,
  });

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
      message.success('Modifier-Gruppe angelegt.');
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
      message.success('Modifier hinzugefügt.');
      setModifierModalOpen(false);
      setSelectedGroup(null);
      modifierForm.resetFields();
      queryClient.invalidateQueries({ queryKey: modifierGroupsKey });
    } catch (e: any) {
      if (e?.errorFields) return;
      message.error('Modifier konnte nicht hinzugefügt werden.');
    }
  };

  const items = groups.map((g) => ({
    key: g.id,
    label: g.name,
    extra: (
      <Button type="link" size="small" onClick={() => openAddModifier(g)}>
        + Modifier
      </Button>
    ),
    children: (
      <ul style={{ margin: 0, paddingLeft: 20 }}>
        {g.modifiers.length === 0 ? (
          <li style={{ color: '#999' }}>Keine Modifier. Klicken Sie auf „+ Modifier“.</li>
        ) : (
          g.modifiers.map((m) => (
            <li key={m.id}>
              {m.name} — €{Number(m.price).toFixed(2)} (MwSt. {m.taxType})
            </li>
          ))
        )}
      </ul>
    ),
  }));

  return (
    <div style={{ padding: 24, background: '#fff', borderRadius: 8 }}>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>Extra Zutaten (Modifier-Gruppen)</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setGroupModalOpen(true)}>
          Gruppe anlegen
        </Button>
      </div>
      <p style={{ color: '#666', marginBottom: 16 }}>
        Gruppieren Sie Modifier (z. B. Saucen, Extras). Anschließend können Sie in Produkten unter „Extra Zutaten“ festlegen, welche Gruppen pro Produkt wählbar sind.
      </p>
      {isLoading ? (
        <div style={{ padding: 24, textAlign: 'center' }}>Laden…</div>
      ) : (
        <Collapse items={items} defaultActiveKey={groups.map((g) => g.id)} />
      )}

      <Modal
        title="Neue Modifier-Gruppe"
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
        title={selectedGroup ? `Modifier in „${selectedGroup.name}“` : 'Modifier hinzufügen'}
        open={modifierModalOpen}
        onOk={handleAddModifier}
        onCancel={() => { setModifierModalOpen(false); setSelectedGroup(null); modifierForm.resetFields(); }}
        okText="Hinzufügen"
      >
        <Form form={modifierForm} layout="vertical" initialValues={{ price: 0, taxType: 1, sortOrder: 0 }}>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}>
            <Input placeholder="z. B. Ketchup, Mayo, Extra Fleisch" />
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
    </div>
  );
}
