'use client';

import { Select, Space, Tag } from 'antd';
import type { SelectProps } from 'antd';
import React, { useMemo } from 'react';

import { useTaxGroups } from '@/hooks/useTaxGroups';
import { useI18n } from '@/i18n';

export type TaxSelectProps = Omit<SelectProps, 'options' | 'optionLabelProp' | 'children'> & {
  /** When true, only active tax groups are listed (default). */
  activeOnly?: boolean;
};

export function TaxSelect({ value, onChange, activeOnly = true, ...props }: TaxSelectProps) {
  const { t } = useI18n();
  const { data: taxGroups, isLoading } = useTaxGroups();

  const options = useMemo(() => {
    const list = (taxGroups ?? []).filter((g) => (activeOnly ? g.isActive : true));
    return list.map((group) => ({
      value: group.id,
      label: (
        <Space size={6}>
          <span>{group.icon}</span>
          <span>{group.name}</span>
          <Tag color={group.color ?? undefined} style={{ marginInlineEnd: 0 }}>
            {group.rate}%
          </Tag>
        </Space>
      ),
      searchLabel: `${group.name} ${group.rate}% ${group.austrianCode ?? ''}`,
      group,
    }));
  }, [taxGroups, activeOnly]);

  return (
    <Select
      value={value}
      onChange={onChange}
      loading={isLoading}
      showSearch
      optionFilterProp="searchLabel"
      optionLabelProp="label"
      placeholder={t('products.form.taxGroupPlaceholder')}
      {...props}
      options={options.map(({ value: v, label, searchLabel, group }) => ({
        value: v,
        label,
        searchLabel,
        group,
      }))}
      optionRender={(option) => {
        const group = (option.data as { group?: (typeof options)[number]['group'] }).group;
        if (!group) return option.label;
        return (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 8,
              width: '100%',
            }}
          >
            <Space size={6} wrap>
              <span>{group.icon}</span>
              <span>{group.name}</span>
              <Tag color={group.color ?? undefined} style={{ marginInlineEnd: 0 }}>
                {group.rate}%
              </Tag>
              {group.austrianCode ? <Tag style={{ marginInlineEnd: 0 }}>{group.austrianCode}</Tag> : null}
            </Space>
            <Space size={4}>
              {group.isDefault ? (
                <Tag color="blue" style={{ marginInlineEnd: 0 }}>
                  {t('settings.taxGroups.default')}
                </Tag>
              ) : null}
              {group.isSystem ? (
                <Tag color="green" style={{ marginInlineEnd: 0 }}>
                  {t('settings.taxGroups.system')}
                </Tag>
              ) : null}
            </Space>
          </div>
        );
      }}
    />
  );
}
