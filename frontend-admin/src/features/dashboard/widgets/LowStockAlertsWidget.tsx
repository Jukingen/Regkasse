'use client';

import { Typography } from 'antd';
import React, { useMemo } from 'react';

import { SimpleList as List } from '@/components/ui/SimpleList';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type InventoryRow = {
  id: string;
  productName?: string;
  currentStock: number;
  minStockLevel: number;
};

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function LowStockAlertsWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t } = useI18n();
  const query = useAuthorizedQuery({
    queryKey: ['dashboard', 'low-stock'],
    queryFn: () =>
      customInstance<InventoryRow[]>({
        url: '/api/Inventory',
        method: 'GET',
      }),
    requiredPermission: PERMISSIONS.INVENTORY_VIEW,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
  });

  const lowStock = useMemo(
    () =>
      (query.data ?? [])
        .filter((r) => r.currentStock < r.minStockLevel)
        .sort((a, b) => a.currentStock - b.currentStock)
        .slice(0, 8),
    [query.data]
  );

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
    >
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
        {t('dashboard.widgets.lowStock.summary', { count: lowStock.length })}
      </Typography.Text>
      <List
        size="small"
        loading={query.isLoading}
        dataSource={lowStock}
        locale={{ emptyText: t('dashboard.widgets.lowStock.empty') }}
        renderItem={(item) => (
          <List.Item>
            <List.Item.Meta
              title={item.productName ?? t('dashboard.widgets.lowStock.productFallback')}
              description={t('dashboard.widgets.lowStock.stockDescription', {
                current: item.currentStock,
                min: item.minStockLevel,
              })}
            />
          </List.Item>
        )}
      />
    </WidgetShell>
  );
}
