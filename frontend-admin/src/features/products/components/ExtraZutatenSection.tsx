'use client';

/**
 * Product Form: „Vorgeschlagene Add-on-Gruppen“ – products-basiert read-only, Modifiers Legacy.
 * Grupları checkbox ile listeler; açıldığında Produkte (Preis/MwSt. aus Product) read-only, optional Modifier (Legacy).
 */

import React from 'react';
import { Checkbox, Collapse, Spin, Typography } from 'antd';
import type { ModifierGroupDto, AddOnGroupProductItemDto } from '@/lib/api/modifierGroups';

const { Text } = Typography;

export interface ExtraZutatenSectionProps {
  /** Tüm Add-on-Gruppen (products + modifiers). */
  groups: ModifierGroupDto[];
  /** Bu ürüne atanmış grup id'leri. */
  selectedGroupIds: string[];
  /** Seçim değiştiğinde. */
  onChange: (selectedGroupIds: string[]) => void;
  /** Yükleniyor (gruplar çekilirken). */
  loading?: boolean;
}

export default function ExtraZutatenSection({
  groups,
  selectedGroupIds,
  onChange,
  loading,
}: ExtraZutatenSectionProps) {
  const toggleGroup = (groupId: string, checked: boolean) => {
    if (checked) {
      onChange([...selectedGroupIds, groupId]);
    } else {
      onChange(selectedGroupIds.filter((id) => id !== groupId));
    }
  };

  const items = groups.map((group) => {
    const products: AddOnGroupProductItemDto[] = group.products ?? [];
    const modifiers = group.modifiers ?? [];
    return {
      key: group.id,
      label: (
        <Checkbox
          checked={selectedGroupIds.includes(group.id)}
          onChange={(e) => toggleGroup(group.id, e.target.checked)}
          onClick={(e) => e.stopPropagation()}
        >
          <Text strong>{group.name}</Text>
        </Checkbox>
      ),
      children: (
        <div style={{ paddingLeft: 24 }}>
          <div style={{ marginBottom: 8, fontSize: 12, color: '#333' }}>Add-on-Produkte in dieser Gruppe</div>
          {products.length === 0 ? (
            <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>Keine Add-on-Produkte in dieser Gruppe.</Text>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20, marginBottom: 12 }}>
              {products.map((p) => (
                <li key={p.productId} style={{ marginBottom: 4 }}>
                  <Text>{p.productName} — €{Number(p.price).toFixed(2)} (MwSt.-Typ {p.taxType})</Text>
                </li>
              ))}
            </ul>
          )}
          <div style={{ fontSize: 12, color: '#999', marginBottom: 4 }}>Modifier (Legacy, nur Leseansicht)</div>
          {modifiers.length === 0 ? (
            <Text type="secondary" style={{ display: 'block' }}>Keine Modifier.</Text>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20 }}>
              {modifiers.map((m) => (
                <li key={m.id} style={{ marginBottom: 4, color: '#666' }}>
                  <Text>{m.name} — €{Number(m.price).toFixed(2)}</Text>
                </li>
              ))}
            </ul>
          )}
        </div>
      ),
    };
  });

  if (loading) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Spin tip="Add-on-Gruppen werden geladen…" />
      </div>
    );
  }

  if (groups.length === 0) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Text type="secondary">Keine Add-on-Gruppen. Legen Sie unter „Add-on-Gruppen“ Gruppen an und fügen Sie Add-on-Produkte hinzu. Anschließend wählen Sie hier, welche Gruppen für dieses Produkt angezeigt werden.</Text>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 8 }}>
      <Collapse
        items={items}
        defaultActiveKey={selectedGroupIds.length > 0 ? selectedGroupIds : [groups[0]?.id].filter(Boolean)}
        style={{ background: '#fafafa' }}
      />
    </div>
  );
}
