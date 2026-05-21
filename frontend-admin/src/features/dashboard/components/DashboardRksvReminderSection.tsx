'use client';

import React from 'react';
import { Card, Skeleton } from 'antd';
import { useLazyWhenVisible } from '@/components/ui/LazyWhenVisible';
import { RksvReminderStatusCard } from '@/features/dashboard/components/RksvReminderStatusCard';

type DashboardRksvReminderSectionProps = {
    enabled: boolean;
};

/** Per-register RKSV reminder queries load when the card enters the viewport. */
export function DashboardRksvReminderSection({ enabled }: DashboardRksvReminderSectionProps) {
    const { ref, visible } = useLazyWhenVisible(enabled);

    if (!enabled) return null;

    return (
        <div ref={ref} style={{ marginBottom: 24 }}>
            {!visible ? (
                <Card>
                    <Skeleton active paragraph={{ rows: 3 }} />
                </Card>
            ) : (
                <RksvReminderStatusCard enabled={enabled} />
            )}
        </div>
    );
}
