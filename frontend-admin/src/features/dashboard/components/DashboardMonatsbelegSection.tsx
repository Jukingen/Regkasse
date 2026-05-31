'use client';

import React from 'react';
import { Card, Skeleton } from 'antd';
import { useLazyWhenVisible } from '@/components/ui/LazyWhenVisible';
import { MonatsbelegWidget } from '@/features/dashboard/components/MonatsbelegWidget';

type DashboardMonatsbelegSectionProps = {
    enabled: boolean;
};

/** Defers Monatsbeleg widget fetch until the section scrolls into view. */
export function DashboardMonatsbelegSection({ enabled }: DashboardMonatsbelegSectionProps) {
    const { ref, visible } = useLazyWhenVisible(enabled);

    if (!enabled) return null;

    return (
        <div ref={ref} style={{ marginBottom: 24 }}>
            {!visible ? (
                <Card>
                    <Skeleton active paragraph={{ rows: 4 }} />
                </Card>
            ) : (
                <MonatsbelegWidget enabled={enabled && visible} />
            )}
        </div>
    );
}
