'use client';

import React from 'react';
import { Card, Skeleton } from 'antd';
import { useLazyWhenVisible } from '@/components/ui/LazyWhenVisible';
import { MonatsbelegComplianceTable } from '@/features/dashboard/components/MonatsbelegComplianceTable';
import { useAdminMonatsbelegOverview } from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';

type DashboardMonatsbelegSectionProps = {
    enabled: boolean;
};

/** Monatsbeleg N+1 status calls start only when the section scrolls into view. */
export function DashboardMonatsbelegSection({ enabled }: DashboardMonatsbelegSectionProps) {
    const { ref, visible } = useLazyWhenVisible(enabled);

    const overview = useAdminMonatsbelegOverview(enabled && visible);

    if (!enabled) return null;

    return (
        <div ref={ref} style={{ marginBottom: 24 }}>
            {!visible ? (
                <Card>
                    <Skeleton active paragraph={{ rows: 4 }} />
                </Card>
            ) : (
                <MonatsbelegComplianceTable
                    rows={overview.rows}
                    loading={overview.registersLoading || overview.statusPending}
                />
            )}
        </div>
    );
}
