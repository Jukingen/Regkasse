'use client';

/**
 * Product Form: Add-on-Gruppen für dieses Produkt auswählen.
 * Product → ModifierGroup: welche Gruppen für dieses Produkt wählbar sind.
 * Add-on-Produkte pro Gruppe werden unter „Add-on-Gruppen“ verwaltet (nicht hier).
 */

import React from 'react';
import { Alert, Checkbox, Collapse, Spin, Typography } from 'antd';
import type { ModifierGroupDto, AddOnGroupProductItemDto } from '@/lib/api/modifierGroups';

const { Text } = Typography;

export interface ExtraZutatenSectionProps {
  /** All add-on groups (from getModifierGroups). Includes products; modifiers only for legacy display subsection. */
  groups: ModifierGroupDto[];
  /** Bu ürüne atanmış grup id'leri. */
  selectedGroupIds: string[];
  /** Seçim değiştiğinde. */
  onChange: (selectedGroupIds: string[]) => void;
  /** Yükleniyor (gruplar çekilirken). */
  loading?: boolean;
}

/** API yanıtında id veya Id gelebilir; her iki durumda da string döndür. */
function getGroupId(g: ModifierGroupDto): string {
  return String((g as { id?: string; Id?: string }).id ?? (g as { id?: string; Id?: string }).Id ?? '');
}

export default function ExtraZutatenSection({
  groups,
  selectedGroupIds,
  onChange,
  loading,
}: ExtraZutatenSectionProps) {
  const toggleGroup = (groupId: string, checked: boolean) => {
    if (!groupId) return;
    if (checked) {
      onChange([...selectedGroupIds, groupId]);
    } else {
      onChange(selectedGroupIds.filter((id) => id !== groupId));
    }
  };

  const emptySelectedGroups = groups.filter(
    (g) => selectedGroupIds.includes(getGroupId(g)) && ((g.products ?? []).length === 0)
  );

  const items = groups.map((group) => {
    const gid = getGroupId(group);
    const products: AddOnGroupProductItemDto[] = group.products ?? [];
    // Legacy-only: modifiers come from getModifierGroups() (not from product modifier-groups response). Phase D PR-D.
    const modifiers = group.modifiers ?? [];
    const productCount = products.length;
    const countLabel = productCount === 0 ? 'leer' : `${productCount} Produkt${productCount !== 1 ? 'e' : ''}`;
    return {
      key: gid,
      label: (
        <Checkbox
          checked={selectedGroupIds.includes(gid)}
          onChange={(e) => toggleGroup(gid, e.target.checked)}
          onClick={(e) => e.stopPropagation()}
        >
          <Text strong>{group.name}</Text>
          <Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
            ({countLabel})
          </Text>
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
      {emptySelectedGroups.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message="Leere Gruppen ausgewählt"
          description={
            <>
              {emptySelectedGroups.map((g) => (
                <div key={getGroupId(g)} style={{ marginBottom: 4 }}>
                  Die Gruppe „{g.name}" enthält keine Add-on-Produkte. Im POS erscheinen keine Extras für diese Gruppe. Fügen Sie unter „Add-on-Gruppen" Produkte hinzu.
                </div>
              ))}
            </>
          }
          style={{ marginBottom: 12 }}
        />
      )}
      <Collapse
        items={items}
        defaultActiveKey={selectedGroupIds.length > 0 ? selectedGroupIds : (groups[0] ? [getGroupId(groups[0])] : [])}
        style={{ background: '#fafafa' }}
      />
    </div>
  );
}
