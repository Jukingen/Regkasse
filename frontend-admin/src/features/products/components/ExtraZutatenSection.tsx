'use client';

/**
 * Product Form: Add-on-Gruppen für dieses Produkt auswählen.
 * Product → ModifierGroup: welche Gruppen für dieses Produkt wählbar sind.
 * Add-on-Produkte pro Gruppe werden unter „Add-on-Gruppen“ verwaltet (nicht hier).
 */

import React from 'react';
import { Alert, Checkbox, Collapse, Spin, Typography } from 'antd';
import type { ModifierGroupDto, AddOnGroupProductItemDto } from '@/lib/api/modifierGroups';
import { useI18n } from '@/i18n';

const { Text } = Typography;

export interface ExtraZutatenSectionProps {
  /** All add-on groups (from getModifierGroups). Uses products as source of truth. */
  groups: ModifierGroupDto[];
  /** Group IDs assigned to this product. */
  selectedGroupIds: string[];
  /** Called when selection changes. */
  onChange: (selectedGroupIds: string[]) => void;
  /** Loading (while groups are being fetched). */
  loading?: boolean;
}

/** API response may return id or Id; return string in both cases. */
function getGroupId(g: ModifierGroupDto): string {
  return String((g as { id?: string; Id?: string }).id ?? (g as { id?: string; Id?: string }).Id ?? '');
}

export default function ExtraZutatenSection({
  groups,
  selectedGroupIds,
  onChange,
  loading,
}: ExtraZutatenSectionProps) {
  const { t } = useI18n();

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
    const productCount = products.length;
    const countLabel =
      productCount === 0
        ? t('products.addonGroups.countEmpty')
        : productCount === 1
          ? t('products.addonGroups.countOne')
          : t('products.addonGroups.countMany', { count: productCount });
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
          <div style={{ marginBottom: 8, fontSize: 12, color: '#333' }}>{t('products.addonGroups.sectionTitle')}</div>
          {products.length === 0 ? (
            <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>{t('products.addonGroups.noneInGroup')}</Text>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 20, marginBottom: 12 }}>
              {products.map((p) => (
                <li key={p.productId} style={{ marginBottom: 4 }}>
                  <Text>
                    {p.productName} — €{Number(p.price).toFixed(2)} ({t('products.addonGroups.taxSuffix', { type: p.taxType })})
                  </Text>
                </li>
              ))}
            </ul>
          )}
          <Text type="secondary" style={{ display: 'block' }}>
            {t('products.addonGroups.runtimeHint')}
          </Text>
        </div>
      ),
    };
  });

  if (loading) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Spin tip={t('products.addonGroups.loading')} />
      </div>
    );
  }

  if (groups.length === 0) {
    return (
      <div style={{ padding: '12px 0' }}>
        <Text type="secondary">{t('products.addonGroups.emptyCatalog')}</Text>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 8 }}>
      {emptySelectedGroups.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={t('products.addonGroups.warningTitle')}
          description={
            <>
              {emptySelectedGroups.map((g) => (
                <div key={getGroupId(g)} style={{ marginBottom: 4 }}>
                  {t('products.addonGroups.warningItem', { name: g.name })}
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
