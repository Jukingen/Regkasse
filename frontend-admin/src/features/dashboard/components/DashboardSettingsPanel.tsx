'use client';

import { Button, Drawer, Switch, Typography } from 'antd';
import React from 'react';

import { SimpleList as List } from '@/components/ui/SimpleList';
import type {
  DashboardWidgetCatalogItem,
  DashboardWidgetPreference,
} from '@/features/dashboard/types';
import { useI18n } from '@/i18n/I18nProvider';

type Props = {
  open: boolean;
  onClose: () => void;
  catalog: DashboardWidgetCatalogItem[];
  widgets: DashboardWidgetPreference[];
  onVisibilityChange: (widgetId: string, isVisible: boolean) => void;
  onResetLayout?: () => void;
};

/** Toggle which widgets appear on the dashboard. */
export function DashboardSettingsPanel({
  open,
  onClose,
  catalog,
  widgets,
  onVisibilityChange,
  onResetLayout,
}: Props) {
  const { t } = useI18n();
  const visibility = new Map(widgets.map((w) => [w.widgetId, w.isVisible]));

  return (
    <Drawer
      title={t('dashboard.customize.drawerTitle')}
      placement="right"
      size={360}
      open={open}
      onClose={onClose}
      extra={
        onResetLayout ? (
          <Button type="link" onClick={onResetLayout} style={{ paddingInline: 0 }}>
            {t('dashboard.customize.resetLayout')}
          </Button>
        ) : null
      }
    >
      <Typography.Paragraph type="secondary">
        {t('dashboard.customize.drawerIntro')}
      </Typography.Paragraph>
      <List
        dataSource={catalog}
        rowKey="widgetId"
        renderItem={(item) => {
          const checked = visibility.get(item.widgetId) ?? item.defaultVisible;
          return (
            <List.Item
              actions={[
                <Switch
                  key="vis"
                  checked={checked}
                  onChange={(v) => onVisibilityChange(item.widgetId, v)}
                  aria-label={t('dashboard.customize.toggleAria', { title: item.title })}
                />,
              ]}
            >
              <List.Item.Meta title={item.title} description={item.description} />
            </List.Item>
          );
        }}
      />
    </Drawer>
  );
}
