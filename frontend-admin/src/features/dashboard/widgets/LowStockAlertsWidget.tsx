'use client';

import React, { useMemo } from 'react';
import { List, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type InventoryRow = {
    id: string;
    productName?: string;
    currentStock: number;
    minStockLevel: number;
};

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function LowStockAlertsWidget({ title, dragHandleProps, onRefresh }: Props) {
    const query = useQuery({
        queryKey: ['dashboard', 'low-stock'],
        queryFn: () =>
            customInstance<InventoryRow[]>({
                url: '/api/Inventory',
                method: 'GET',
            }),
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    });

    const lowStock = useMemo(
        () =>
            (query.data ?? [])
                .filter((r) => r.currentStock < r.minStockLevel)
                .sort((a, b) => a.currentStock - b.currentStock)
                .slice(0, 8),
        [query.data],
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
                {lowStock.length} Artikel unter Mindestbestand
            </Typography.Text>
            <List
                size="small"
                loading={query.isLoading}
                dataSource={lowStock}
                locale={{ emptyText: 'Keine Warnungen' }}
                renderItem={(item) => (
                    <List.Item>
                        <List.Item.Meta
                            title={item.productName ?? 'Produkt'}
                            description={`Bestand: ${item.currentStock} / Min: ${item.minStockLevel}`}
                        />
                    </List.Item>
                )}
            />
        </WidgetShell>
    );
}
