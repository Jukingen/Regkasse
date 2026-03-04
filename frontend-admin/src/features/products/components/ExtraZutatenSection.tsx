'use client';

/**
 * Product Form içinde "Extra Zutaten" bölümü.
 * Modifier gruplarını checkbox ile listeler; grup açıldığında modifier'ları (ad + fiyat) read-only gösterir.
 */

import React from 'react';
import { Checkbox, Collapse, Spin, Typography } from 'antd';
import type { ModifierGroupDto } from '@/lib/api/modifierGroups';

const { Text } = Typography;

export interface ExtraZutatenSectionProps {
  /** Tüm modifier grupları (modifier listesi ile). */
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

  const items = groups.map((group) => ({
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
        {group.modifiers.length === 0 ? (
          <Text type="secondary">Keine Modifier in dieser Gruppe.</Text>
        ) : (
          <ul style={{ margin: 0, paddingLeft: 20 }}>
            {group.modifiers.map((m) => (
              <li key={m.id} style={{ marginBottom: 4 }}>
                <Text>
                  {m.name} — €{Number(m.price).toFixed(2)}
                </Text>
              </li>
            ))}
          </ul>
        )}
      </div>
    ),
  }));

  if (loading) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Spin tip="Extra Zutaten werden geladen…" />
      </div>
    );
  }

  if (groups.length === 0) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Text type="secondary">Keine Modifier-Gruppen angelegt. Legen Sie zuerst unter Einstellungen oder Modifier-Gruppen Gruppen an.</Text>
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
